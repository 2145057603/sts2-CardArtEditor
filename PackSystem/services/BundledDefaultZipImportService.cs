using Godot;
using InfectionCardReplacer.PackSystem.Models;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class BundledDefaultZipImportService
{
    private const string BundledZipDirectory = "res://packs/default_zip_packages";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly FlexibleZipPackImportService _zipImportService = new();
    private readonly PackRegistryService _registryService = new();
    private readonly UserPackStateService _userPackStateService = new();

    public void EnsureImported()
    {
        DirAccess? directory = DirAccess.Open(BundledZipDirectory);
        if (directory is null)
        {
            return;
        }

        List<BundledDefaultZipImportState> states = LoadStates();
        Dictionary<string, BundledDefaultZipImportState> stateLookup = states
            .ToDictionary(item => item.ResourcePath, StringComparer.OrdinalIgnoreCase);
        HashSet<string> importedPackIds = _registryService
            .ScanImportedPacks()
            .Select(item => item.PackId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool changed = false;
        directory.ListDirBegin();
        try
        {
            while (true)
            {
                string fileName = directory.GetNext();
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    break;
                }

                if (directory.CurrentIsDir() || !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string resourcePath = $"{BundledZipDirectory}/{fileName}";
                byte[]? zipBytes = ReadResourceBytes(resourcePath);
                if (zipBytes is null || zipBytes.Length == 0)
                {
                    GD.Print($"[CardArtEditor][Pack] Skip empty bundled zip: {resourcePath}");
                    continue;
                }

                string contentHash = Convert.ToHexString(SHA256.HashData(zipBytes));
                if (CanSkipImport(resourcePath, contentHash, stateLookup, importedPackIds))
                {
                    continue;
                }

                string tempZipPath = WriteTempZip(resourcePath, fileName, zipBytes);
                try
                {
                    ZipPackImportResult result = _zipImportService.ImportFromZip(tempZipPath);
                    if (!result.IsSuccess)
                    {
                        GD.Print($"[CardArtEditor][Pack] Bundled zip import failed: {fileName} | {result.Message}");
                        continue;
                    }

                    // 默认内置包导入成功后直接保持启用，避免玩家第一次打开时还要再手动启用。
                    if (!string.IsNullOrWhiteSpace(result.PackId))
                    {
                        _userPackStateService.SetEnabledForSelection(result.PackId, true);
                        importedPackIds.Add(result.PackId);
                    }

                    stateLookup[resourcePath] = new BundledDefaultZipImportState
                    {
                        ResourcePath = resourcePath,
                        FileName = fileName,
                        ContentHash = contentHash,
                        ImportedPackId = result.PackId,
                        ImportedAtUtc = DateTime.UtcNow
                    };
                    changed = true;

                    GD.Print($"[CardArtEditor][Pack] Bundled zip imported: {fileName} -> {result.PackId}");
                }
                finally
                {
                    CleanupTempZip(tempZipPath);
                }
            }
        }
        finally
        {
            directory.ListDirEnd();
        }

        if (!changed)
        {
            return;
        }

        SaveStates(stateLookup.Values
            .OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private bool CanSkipImport(
        string resourcePath,
        string contentHash,
        IReadOnlyDictionary<string, BundledDefaultZipImportState> stateLookup,
        IReadOnlySet<string> importedPackIds)
    {
        if (!stateLookup.TryGetValue(resourcePath, out BundledDefaultZipImportState? state))
        {
            return false;
        }

        if (!string.Equals(state.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(state.ImportedPackId))
        {
            return false;
        }

        return importedPackIds.Contains(state.ImportedPackId);
    }

    private static byte[]? ReadResourceBytes(string resourcePath)
    {
        return Godot.FileAccess.GetFileAsBytes(resourcePath);
    }

    private static string WriteTempZip(string resourcePath, string fileName, byte[] zipBytes)
    {
        string tempRoot = ProjectSettings.GlobalizePath("user://pack_work/default_zip_import");
        Directory.CreateDirectory(tempRoot);

        string safeFileName = string.IsNullOrWhiteSpace(fileName) ? "bundled_default.zip" : fileName;
        string hashPrefix = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(resourcePath)))[..8].ToLowerInvariant();
        string tempPath = Path.Combine(tempRoot, $"{hashPrefix}_{safeFileName}");
        File.WriteAllBytes(tempPath, zipBytes);
        return tempPath;
    }

    private static void CleanupTempZip(string tempZipPath)
    {
        if (string.IsNullOrWhiteSpace(tempZipPath) || !File.Exists(tempZipPath))
        {
            return;
        }

        try
        {
            File.Delete(tempZipPath);
        }
        catch
        {
            // 临时 ZIP 清理失败不影响主流程，下次导入会直接覆盖同名临时文件。
        }
    }

    private static List<BundledDefaultZipImportState> LoadStates()
    {
        string path = GetStatePath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            List<BundledDefaultZipImportState>? items = JsonSerializer.Deserialize<List<BundledDefaultZipImportState>>(
                File.ReadAllText(path),
                JsonOptions);
            return items ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveStates(List<BundledDefaultZipImportState> items)
    {
        string path = GetStatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ProjectSettings.GlobalizePath("user://"));
        File.WriteAllText(path, JsonSerializer.Serialize(items, JsonOptions));
    }

    private static string GetStatePath()
    {
        return ProjectSettings.GlobalizePath("user://pack_state/bundled_default_zip_import_state.json");
    }
}

internal sealed class BundledDefaultZipImportState
{
    public string ResourcePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string ImportedPackId { get; set; } = string.Empty;
    public DateTime ImportedAtUtc { get; set; }
}
