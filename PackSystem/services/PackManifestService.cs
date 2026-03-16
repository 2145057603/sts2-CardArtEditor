using InfectionCardReplacer.PackSystem.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class PackManifestService
{
    private static readonly Regex PackIdRegex = new("^[a-z0-9][a-z0-9._-]{2,63}$", RegexOptions.Compiled);
    private static readonly Regex SemVerRegex = new(
        "^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public PackManifestLoadResult LoadFromDirectory(string rootDirectory)
    {
        PackManifestLoadResult result = new();
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            result.Issues.Add(new PackValidationIssue
            {
                Code = "pack.root_missing",
                Message = $"卡包根目录不存在：{rootDirectory}"
            });
            return result;
        }

        string manifestPath = Path.Combine(rootDirectory, "pack.json");
        if (!File.Exists(manifestPath))
        {
            result.Issues.Add(new PackValidationIssue
            {
                Code = "pack.manifest_missing",
                Message = "缺少 pack.json。"
            });
            return result;
        }

        PackManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PackManifest>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (Exception ex)
        {
            result.Issues.Add(new PackValidationIssue
            {
                Code = "pack.manifest_invalid_json",
                Message = $"pack.json 解析失败：{ex.GetBaseException().Message}"
            });
            return result;
        }

        if (manifest is null)
        {
            result.Issues.Add(new PackValidationIssue
            {
                Code = "pack.manifest_empty",
                Message = "pack.json 内容为空或无法识别。"
            });
            return result;
        }

        result.Manifest = manifest;
        ValidateManifest(rootDirectory, manifest, result);
        if (result.Issues.Count > 0 && result.Issues.Any(issue => issue.Code.StartsWith("pack.", StringComparison.Ordinal)))
        {
            return result;
        }

        TryLoadMappings(rootDirectory, manifest, result);
        result.IsSuccess = result.Manifest is not null &&
            result.Issues.All(issue => !issue.Code.StartsWith("pack.", StringComparison.Ordinal)) &&
            (!RequiresMappings(result.Manifest.PackType) || result.MappingFile is not null || result.IsDegraded);
        return result;
    }

    private static void ValidateManifest(string rootDirectory, PackManifest manifest, PackManifestLoadResult result)
    {
        // 这里先把“协议层必须成立”的规则一次性卡死，避免后续不同模块各自放宽标准。
        if (manifest.FormatVersion != 1)
        {
            result.Issues.Add(new PackValidationIssue
            {
                Code = "pack.format_version_unsupported",
                Message = $"不支持的格式版本：{manifest.FormatVersion}"
            });
        }

        if (!PackIdRegex.IsMatch(manifest.PackId))
        {
            result.Issues.Add(new PackValidationIssue
            {
                Code = "pack.pack_id_invalid",
                Message = "packId 不符合规范。"
            });
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName) || manifest.DisplayName.Length > 80)
        {
            result.Issues.Add(new PackValidationIssue
            {
                Code = "pack.display_name_invalid",
                Message = "displayName 不能为空且长度不能超过 80。"
            });
        }

        if (!SemVerRegex.IsMatch(manifest.Version))
        {
            result.Issues.Add(new PackValidationIssue
            {
                Code = "pack.version_invalid",
                Message = "version 必须是合法 SemVer。"
            });
        }

        if (!IsSafeRelativePath(manifest.EntryRoot))
        {
            result.Issues.Add(new PackValidationIssue
            {
                Code = "pack.entry_root_invalid",
                Message = "entryRoot 必须是安全的相对路径。"
            });
        }
        else
        {
            string entryRootPath = Path.Combine(rootDirectory, manifest.EntryRoot);
            if (!Directory.Exists(entryRootPath))
            {
                result.Issues.Add(new PackValidationIssue
                {
                    Code = "pack.entry_root_missing",
                    Message = "entryRoot 指向的目录不存在。"
                });
            }
            else if (!EnumerateImageFiles(entryRootPath).Any())
            {
                result.Issues.Add(new PackValidationIssue
                {
                    Code = "pack.entry_root_empty",
                    Message = "entryRoot 下没有可用图片。"
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(manifest.Cover))
        {
            ValidateRelativeFile(rootDirectory, manifest.Cover, "pack.cover_invalid", "cover 指向的文件不存在或路径不合法。", result);
        }

        if (!string.IsNullOrWhiteSpace(manifest.MappingsFile))
        {
            ValidateRelativeFile(rootDirectory, manifest.MappingsFile, "pack.mappings_file_invalid", "mappingsFile 指向的文件不存在或路径不合法。", result);
        }

        manifest.Tags = manifest.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToList();

        if (manifest.Priority is < 0 or > 1000)
        {
            result.Issues.Add(new PackValidationIssue
            {
                Code = "pack.priority_invalid",
                Message = "priority 必须在 0 到 1000 之间。"
            });
        }
    }

    private void TryLoadMappings(string rootDirectory, PackManifest manifest, PackManifestLoadResult result)
    {
        bool requiresMappings = RequiresMappings(manifest.PackType);
        if (!requiresMappings && string.IsNullOrWhiteSpace(manifest.MappingsFile))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(manifest.MappingsFile))
        {
            if (manifest.PackType == PackType.HybridPack)
            {
                result.IsDegraded = true;
                result.Issues.Add(new PackValidationIssue
                {
                    Code = "mapping.hybrid_missing",
                    Message = "hybrid_pack 缺少 mappingsFile，已降级为 asset 行为。"
                });
                return;
            }

            result.Issues.Add(new PackValidationIssue
            {
                Code = "mapping.file_missing",
                Message = "mapping_pack 必须提供 mappingsFile。"
            });
            return;
        }

        string mappingsPath = Path.Combine(rootDirectory, manifest.MappingsFile);
        PackMappingFile? mappingFile;
        try
        {
            mappingFile = JsonSerializer.Deserialize<PackMappingFile>(File.ReadAllText(mappingsPath), JsonOptions);
        }
        catch (Exception ex)
        {
            HandleMappingFailure(manifest, result, $"映射文件解析失败：{ex.GetBaseException().Message}");
            return;
        }

        if (mappingFile is null)
        {
            HandleMappingFailure(manifest, result, "映射文件内容为空。");
            return;
        }

        if (!ValidateMappingFile(rootDirectory, manifest, mappingFile, result))
        {
            return;
        }

        result.MappingFile = mappingFile;
    }

    private bool ValidateMappingFile(string rootDirectory, PackManifest manifest, PackMappingFile mappingFile, PackManifestLoadResult result)
    {
        if (mappingFile.Entries.Count == 0)
        {
            HandleMappingFailure(manifest, result, "映射文件 entries 不能为空。");
            return false;
        }

        HashSet<string> seenCardIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (PackMappingEntry entry in mappingFile.Entries)
        {
            // 映射文件首版采用严格校验，宁愿整份映射失效，也不允许出现“部分条目偷偷生效”的灰色状态。
            if (string.IsNullOrWhiteSpace(entry.CardId))
            {
                HandleMappingFailure(manifest, result, "映射条目的 cardId 不能为空。");
                return false;
            }

            if (!seenCardIds.Add(entry.CardId.Trim()))
            {
                HandleMappingFailure(manifest, result, $"映射文件中存在重复 cardId：{entry.CardId}");
                return false;
            }

            if (!IsSafeRelativePath(entry.Image))
            {
                HandleMappingFailure(manifest, result, $"映射条目的 image 路径非法：{entry.Image}");
                return false;
            }

            string entryRootPath = Path.Combine(rootDirectory, manifest.EntryRoot);
            string fullImagePath = Path.GetFullPath(Path.Combine(rootDirectory, entry.Image));
            string fullEntryRoot = Path.GetFullPath(entryRootPath);
            if (!fullImagePath.StartsWith(fullEntryRoot, StringComparison.OrdinalIgnoreCase))
            {
                HandleMappingFailure(manifest, result, $"映射条目的 image 越出 entryRoot：{entry.Image}");
                return false;
            }

            if (!File.Exists(fullImagePath))
            {
                HandleMappingFailure(manifest, result, $"映射条目的 image 不存在：{entry.Image}");
                return false;
            }
        }

        return true;
    }

    private static void HandleMappingFailure(PackManifest manifest, PackManifestLoadResult result, string message)
    {
        if (manifest.PackType == PackType.HybridPack)
        {
            result.IsDegraded = true;
            result.Issues.Add(new PackValidationIssue
            {
                Code = "mapping.hybrid_degraded",
                Message = $"{message} 已降级为 asset 行为。"
            });
            return;
        }

        result.Issues.Add(new PackValidationIssue
        {
            Code = "mapping.invalid",
            Message = message
        });
    }

    private static void ValidateRelativeFile(string rootDirectory, string relativePath, string code, string message, PackManifestLoadResult result)
    {
        if (!IsSafeRelativePath(relativePath))
        {
            result.Issues.Add(new PackValidationIssue { Code = code, Message = message });
            return;
        }

        string fullPath = Path.Combine(rootDirectory, relativePath);
        if (!File.Exists(fullPath))
        {
            result.Issues.Add(new PackValidationIssue { Code = code, Message = message });
        }
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            return false;
        }

        string normalized = path.Replace('\\', '/');
        return !normalized.StartsWith("../", StringComparison.Ordinal) &&
               !normalized.Contains("/../", StringComparison.Ordinal) &&
               !normalized.Contains(":");
    }

    private static bool RequiresMappings(PackType packType)
    {
        return packType is PackType.MappingPack or PackType.HybridPack;
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
            });
    }
}
