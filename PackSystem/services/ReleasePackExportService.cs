using InfectionCardReplacer.PackSystem.Models;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class ReleasePackExportService
{
    private static readonly Regex InvalidFileCharsRegex = new("""[^a-zA-Z0-9._-]+""", RegexOptions.Compiled);

    public PackExportResult ExportCurrentReplacements(string destinationPath)
    {
        List<CardArtOverrideRegistry.OverrideExportEntry> entries = CardArtOverrideRegistry.GetOverrideExportEntries()
            .Where(entry => File.Exists(entry.ImagePath))
            .ToList();

        if (entries.Count == 0)
        {
            return Fail("当前没有可导出的已替换卡牌。");
        }

        string resolvedPath = NormalizeDestinationPath(destinationPath);
        if (!string.Equals(Path.GetExtension(resolvedPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("发布包当前仅支持导出为 .zip。");
        }

        string packId = $"release-pack-{DateTime.Now:yyyyMMddHHmmss}";
        string displayName = $"CardArtRelease_{DateTime.Now:yyyyMMdd_HHmmss}";
        string stagingRoot = Path.Combine(Path.GetTempPath(), "CardArtReleaseExport", Guid.NewGuid().ToString("N"));
        string packRoot = Path.Combine(stagingRoot, packId);
        string imagesRoot = Path.Combine(packRoot, "images");
        string mappingsRoot = Path.Combine(packRoot, "mappings");

        try
        {
            Directory.CreateDirectory(imagesRoot);
            Directory.CreateDirectory(mappingsRoot);

            List<PackMappingEntry> mappingEntries = [];
            string coverRelativePath = string.Empty;

            foreach (CardArtOverrideRegistry.OverrideExportEntry entry in entries)
            {
                string cardId = entry.Target.Trim();
                CardCatalogRegistry.Entry? catalogEntry = CardCatalogRegistry.GetEntries()
                    .FirstOrDefault(item => item.FullTypeName.Equals(cardId, StringComparison.OrdinalIgnoreCase));

                string roleFolder = SanitizePathSegment(catalogEntry?.RoleName ?? "unknown");
                string extension = Path.GetExtension(entry.ImagePath);
                string fileName = $"{SanitizePathSegment(GetStableCardFileName(cardId, catalogEntry))}{extension}";
                string relativeImagePath = Path.Combine("images", roleFolder, fileName).Replace("\\", "/", StringComparison.Ordinal);
                string targetImagePath = Path.Combine(packRoot, relativeImagePath.Replace('/', Path.DirectorySeparatorChar));

                string? targetDirectory = Path.GetDirectoryName(targetImagePath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Copy(entry.ImagePath, targetImagePath, overwrite: true);
                if (string.IsNullOrWhiteSpace(coverRelativePath))
                {
                    coverRelativePath = relativeImagePath;
                }

                mappingEntries.Add(new PackMappingEntry
                {
                    CardId = cardId,
                    Image = relativeImagePath,
                    Note = "exported-from-current-replacements"
                });
            }

            PackManifest manifest = new()
            {
                FormatVersion = 1,
                PackId = packId,
                DisplayName = displayName,
                PackType = PackType.MappingPack,
                Version = "1.0.0",
                Author = Environment.UserName,
                Description = "从当前已替换卡牌结果导出的发布包",
                Cover = coverRelativePath,
                EntryRoot = "images",
                Tags = ["release-export", "generated"],
                Priority = 100,
                MappingsFile = "mappings/cards.json"
            };

            JsonSerializerOptions options = new()
            {
                WriteIndented = true
            };

            File.WriteAllText(Path.Combine(packRoot, "pack.json"), JsonSerializer.Serialize(manifest, options));
            File.WriteAllText(Path.Combine(mappingsRoot, "cards.json"), JsonSerializer.Serialize(new PackMappingFile
            {
                Entries = mappingEntries
            }, options));
            File.WriteAllText(Path.Combine(packRoot, "README.txt"),
                "这是从当前已替换卡牌结果导出的发布包。\n导入卡面管理器后启用即可使用。");

            string? destinationDirectory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            if (File.Exists(resolvedPath))
            {
                File.Delete(resolvedPath);
            }

            // 发布包必须是自包含结果，因此这里统一把图片和映射一起打进 ZIP。
            ZipFile.CreateFromDirectory(stagingRoot, resolvedPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            return new PackExportResult
            {
                IsSuccess = true,
                OutputPath = resolvedPath,
                Message = $"已导出发布包：{Path.GetFileName(resolvedPath)}"
            };
        }
        catch (Exception ex)
        {
            return Fail($"导出发布包失败：{ex.GetBaseException().Message}");
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    public static string BuildSuggestedFileName()
    {
        return $"CardArtRelease_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
    }

    private static string GetStableCardFileName(string cardId, CardCatalogRegistry.Entry? catalogEntry)
    {
        if (!string.IsNullOrWhiteSpace(catalogEntry?.TypeName))
        {
            return catalogEntry.TypeName;
        }

        int lastDot = cardId.LastIndexOf('.');
        return lastDot >= 0 && lastDot < cardId.Length - 1 ? cardId[(lastDot + 1)..] : cardId;
    }

    private static string SanitizePathSegment(string text)
    {
        string normalized = InvalidFileCharsRegex.Replace((text ?? string.Empty).Trim(), "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized.ToLowerInvariant();
    }

    private static string NormalizeDestinationPath(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return string.Empty;
        }

        return Path.HasExtension(destinationPath)
            ? destinationPath
            : $"{destinationPath}.zip";
    }

    private static void TryDeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static PackExportResult Fail(string message)
    {
        return new PackExportResult
        {
            IsSuccess = false,
            Message = message
        };
    }
}
