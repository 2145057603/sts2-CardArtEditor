using Godot;
using InfectionCardReplacer.PackSystem.Models;
using System.IO.Compression;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class ReleasePackImportService
{
    private readonly FlexibleZipPackImportService _flexibleImportService = new();
    private readonly PackManifestService _manifestService = new();

    public ZipPackImportResult ImportReleasePack(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            return new ZipPackImportResult
            {
                IsSuccess = false,
                Action = PackImportAction.Rejected,
                Message = "未找到要导入的发布包 ZIP。"
            };
        }

        if (!string.Equals(Path.GetExtension(zipPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new ZipPackImportResult
            {
                IsSuccess = false,
                Action = PackImportAction.Rejected,
                Message = "发布包当前仅支持 .zip 格式。"
            };
        }

        string tempRoot = Path.Combine(
            ProjectSettings.GlobalizePath("user://pack_work"),
            "release_precheck",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);
            SafeExtract(zipPath, tempRoot);

            string? packRoot = ResolvePackRoot(tempRoot);
            if (string.IsNullOrWhiteSpace(packRoot))
            {
                return Fail("发布包必须包含 pack.json。");
            }

            PackManifestLoadResult loadResult = _manifestService.LoadFromDirectory(packRoot);
            if (!loadResult.IsSuccess || loadResult.Manifest is null)
            {
                string message = loadResult.Issues.Count > 0
                    ? string.Join(" | ", loadResult.Issues.Select(issue => issue.Message))
                    : "发布包协议校验失败。";
                return Fail(message);
            }

            // 发布包必须是带映射的成品包，不能让纯图片 ZIP 混进这条流程。
            if (loadResult.Manifest.PackType == PackType.AssetPack)
            {
                return Fail("发布包不能是 asset_pack，必须包含卡牌映射。");
            }

            if (loadResult.MappingFile is null || loadResult.IsDegraded)
            {
                return Fail("发布包必须包含完整可用的 mappings/cards.json。");
            }

            return _flexibleImportService.ImportFromZip(zipPath);
        }
        catch (Exception ex)
        {
            return Fail($"发布包导入失败：{ex.GetBaseException().Message}");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string? ResolvePackRoot(string extractRoot)
    {
        string directManifest = Path.Combine(extractRoot, "pack.json");
        if (File.Exists(directManifest))
        {
            return extractRoot;
        }

        List<string> manifestCandidates = Directory.GetDirectories(extractRoot)
            .Where(directory => File.Exists(Path.Combine(directory, "pack.json")))
            .ToList();
        return manifestCandidates.Count == 1 ? manifestCandidates[0] : null;
    }

    private static void SafeExtract(string zipPath, string extractRoot)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string normalized = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized) ||
                normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.Contains("../", StringComparison.Ordinal) ||
                normalized.Contains("..\\", StringComparison.Ordinal) ||
                normalized.Contains(':', StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"发布包存在非法路径：{entry.FullName}");
            }

            string destinationPath = Path.Combine(extractRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
            if (normalized.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            using Stream input = entry.Open();
            using FileStream output = File.Create(destinationPath);
            input.CopyTo(output);
        }
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

    private static ZipPackImportResult Fail(string message)
    {
        return new ZipPackImportResult
        {
            IsSuccess = false,
            Action = PackImportAction.Rejected,
            Message = message
        };
    }
}
