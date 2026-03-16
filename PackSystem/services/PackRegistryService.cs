using Godot;
using InfectionCardReplacer.PackSystem.Models;
using System.Text.Json;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class PackRegistryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly PackManifestService _manifestService = new();

    public List<PackDescriptor> ScanAvailablePacks(bool includeLooseFolder = false)
    {
        List<PackDescriptor> descriptors = [];
        descriptors.AddRange(ScanBundledPacks());
        descriptors.AddRange(ScanImportedPacks());

        if (includeLooseFolder)
        {
            descriptors.AddRange(ScanLooseFolderPacks());
        }

        return MergeDescriptorsByPackId(descriptors);
    }

    public IReadOnlyList<PackDescriptor> ScanBundledPacks()
    {
        string registryPath = ProjectSettings.GlobalizePath("res://packs/registry.json");
        if (!File.Exists(registryPath))
        {
            return [];
        }

        BundledPackRegistry? registry;
        try
        {
            registry = JsonSerializer.Deserialize<BundledPackRegistry>(File.ReadAllText(registryPath), JsonOptions);
        }
        catch
        {
            return [];
        }

        if (registry?.Packs is null || registry.Packs.Count == 0)
        {
            return [];
        }

        List<PackDescriptor> descriptors = [];
        foreach (string resPath in registry.Packs.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            string rootDirectory = ProjectSettings.GlobalizePath(resPath);
            PackDescriptor? descriptor = TryBuildDescriptor(rootDirectory, PackSourceType.Bundled);
            if (descriptor is not null)
            {
                descriptors.Add(descriptor);
            }
        }

        return descriptors;
    }

    public IReadOnlyList<PackDescriptor> ScanImportedPacks()
    {
        string importedRoot = GetImportedPacksRoot();
        if (!Directory.Exists(importedRoot))
        {
            return [];
        }

        List<PackDescriptor> descriptors = [];
        foreach (string directory in Directory.GetDirectories(importedRoot))
        {
            PackDescriptor? descriptor = TryBuildDescriptor(directory, PackSourceType.Imported);
            if (descriptor is not null)
            {
                descriptors.Add(descriptor);
            }
        }

        return descriptors;
    }

    public IReadOnlyList<PackDescriptor> ScanLooseFolderPacks()
    {
        string looseRoot = GetLooseFolderRoot();
        if (!Directory.Exists(looseRoot))
        {
            return [];
        }

        List<PackDescriptor> descriptors = [];
        foreach (string directory in Directory.GetDirectories(looseRoot))
        {
            PackDescriptor? descriptor = TryBuildDescriptor(directory, PackSourceType.LooseFolder);
            if (descriptor is not null)
            {
                descriptors.Add(descriptor);
            }
        }

        return descriptors;
    }

    public string GetImportedPacksRoot()
    {
        return ProjectSettings.GlobalizePath("user://packs");
    }

    public string GetLooseFolderRoot()
    {
        string? assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        return Path.Combine(assemblyDir ?? ".", "LoosePacks");
    }

    private PackDescriptor? TryBuildDescriptor(string rootDirectory, PackSourceType sourceType)
    {
        PackManifestLoadResult loadResult = _manifestService.LoadFromDirectory(rootDirectory);
        if (!loadResult.IsSuccess || loadResult.Manifest is null)
        {
            return null;
        }

        PackManifest manifest = loadResult.Manifest;
        string coverPath = string.IsNullOrWhiteSpace(manifest.Cover)
            ? string.Empty
            : Path.Combine(rootDirectory, manifest.Cover);

        return new PackDescriptor
        {
            PackId = manifest.PackId,
            DisplayName = manifest.DisplayName,
            Version = manifest.Version,
            PackType = manifest.PackType,
            SourceType = sourceType,
            RootPath = rootDirectory,
            ManifestPath = Path.Combine(rootDirectory, "pack.json"),
            CoverPath = coverPath,
            Author = manifest.Author,
            Description = manifest.Description,
            Priority = manifest.Priority,
            Tags = manifest.Tags.ToList(),
            IsDegraded = loadResult.IsDegraded
        };
    }

    private static List<PackDescriptor> MergeDescriptorsByPackId(List<PackDescriptor> descriptors)
    {
        Dictionary<string, PackDescriptor> merged = new(StringComparer.OrdinalIgnoreCase);
        foreach (PackDescriptor descriptor in descriptors)
        {
            if (!merged.TryGetValue(descriptor.PackId, out PackDescriptor? existing))
            {
                merged[descriptor.PackId] = descriptor;
                continue;
            }

            // 这里先把同 packId 的合并策略定成“导入包优先于内置包”，保证用户升级和覆盖行为可预测。
            if (CompareDescriptorPriority(descriptor, existing) > 0)
            {
                merged[descriptor.PackId] = descriptor;
            }
        }

        return merged.Values
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.PackId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int CompareDescriptorPriority(PackDescriptor left, PackDescriptor right)
    {
        int sourceScoreLeft = GetSourcePriority(left.SourceType);
        int sourceScoreRight = GetSourcePriority(right.SourceType);
        int sourceCompare = sourceScoreLeft.CompareTo(sourceScoreRight);
        if (sourceCompare != 0)
        {
            return sourceCompare;
        }

        return CompareSemVer(left.Version, right.Version);
    }

    private static int GetSourcePriority(PackSourceType sourceType)
    {
        return sourceType switch
        {
            PackSourceType.Imported => 3,
            PackSourceType.LooseFolder => 2,
            _ => 1
        };
    }

    private static int CompareSemVer(string left, string right)
    {
        Version leftVersion = NormalizeVersion(left);
        Version rightVersion = NormalizeVersion(right);
        return leftVersion.CompareTo(rightVersion);
    }

    private static Version NormalizeVersion(string version)
    {
        string normalized = version.Split('-', 2)[0];
        return Version.TryParse(normalized, out Version? parsed)
            ? parsed
            : new Version(0, 0, 0);
    }
}
