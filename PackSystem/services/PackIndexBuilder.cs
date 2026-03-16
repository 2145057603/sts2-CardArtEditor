using Godot;
using InfectionCardReplacer.PackSystem.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class PackIndexBuilder
{
    private readonly PackManifestService _manifestService = new();

    public PackIndex? BuildIndex(PackDescriptor descriptor)
    {
        if (descriptor is null || string.IsNullOrWhiteSpace(descriptor.RootPath) || !Directory.Exists(descriptor.RootPath))
        {
            return null;
        }

        PackManifestLoadResult loadResult = _manifestService.LoadFromDirectory(descriptor.RootPath);
        if (!loadResult.IsSuccess || loadResult.Manifest is null)
        {
            return null;
        }

        PackManifest manifest = loadResult.Manifest;
        string entryRootPath = Path.Combine(descriptor.RootPath, manifest.EntryRoot);
        if (!Directory.Exists(entryRootPath))
        {
            return null;
        }

        PackIndex index = new()
        {
            Descriptor = descriptor
        };

        foreach (string filePath in EnumerateImageFiles(entryRootPath))
        {
            index.Entries.Add(CreateEntry(descriptor, entryRootPath, filePath));
        }

        if (loadResult.MappingFile is not null)
        {
            // 映射文件的协议校验已在导入阶段完成，这里只负责转成运行时索引。
            foreach (PackMappingEntry mapping in loadResult.MappingFile.Entries)
            {
                index.DefaultMappings[mapping.CardId] = NormalizeRelativePath(mapping.Image);
            }
        }

        return index;
    }

    private static IEnumerable<string> EnumerateImageFiles(string rootDirectory)
    {
        return Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path =>
            {
                string extension = Path.GetExtension(path);
                return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                       extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                       extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                       extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static PackEntry CreateEntry(PackDescriptor descriptor, string entryRootPath, string filePath)
    {
        string relativePath = NormalizeRelativePath(Path.GetRelativePath(entryRootPath, filePath));
        string folder = NormalizeRelativePath(Path.GetDirectoryName(relativePath) ?? string.Empty);
        if (folder == ".")
        {
            folder = string.Empty;
        }

        (int width, int height) = ReadImageSize(filePath);
        return new PackEntry
        {
            EntryId = $"{descriptor.PackId}/{relativePath}",
            PackId = descriptor.PackId,
            AbsolutePath = filePath,
            RelativePath = relativePath,
            Folder = folder,
            FileName = Path.GetFileName(filePath),
            Kind = "image",
            Width = width,
            Height = height,
            ThumbnailCacheKey = BuildThumbnailCacheKey(
                descriptor.PackId,
                descriptor.Version,
                relativePath,
                File.GetLastWriteTimeUtc(filePath).Ticks)
        };
    }

    private static (int width, int height) ReadImageSize(string filePath)
    {
        try
        {
            Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            if (image.Load(filePath) != Error.Ok)
            {
                return (0, 0);
            }

            return (image.GetWidth(), image.GetHeight());
        }
        catch
        {
            return (0, 0);
        }
    }

    private static string BuildThumbnailCacheKey(string packId, string version, string relativePath, long lastWriteTicks)
    {
        // 把文件修改时间纳入缓存键，避免“同版本重装但图片变了”时继续命中旧缩略图。
        string normalizedPath = NormalizeRelativePath(relativePath)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("\\", "_", StringComparison.Ordinal);
        return $"{packId}_{version}_{lastWriteTicks}_{normalizedPath}";
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace("\\", "/", StringComparison.Ordinal);
    }
}
