using Godot;
using InfectionCardReplacer.PackSystem.Models;
using System.IO.Compression;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class ZipPackImportService
{
    private const int MaxArchiveEntryCount = 10000;
    private const long MaxArchiveBytes = 512L * 1024L * 1024L;

    private readonly PackManifestService _manifestService = new();
    private readonly PackRegistryService _registryService = new();
    private readonly InstalledPackStateService _installedStateService = new();

    public ZipPackImportResult ImportFromZip(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
        {
            return Fail("未找到要导入的 ZIP 文件。");
        }

        if (!string.Equals(Path.GetExtension(zipPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("当前只支持导入 .zip 格式卡包。");
        }

        string workspaceRoot = ProjectSettings.GlobalizePath("user://pack_work");
        string extractRoot = Path.Combine(workspaceRoot, "extract", Guid.NewGuid().ToString("N"));
        string backupRoot = Path.Combine(workspaceRoot, "backup", Guid.NewGuid().ToString("N"));

        string? importedPackId = null;
        string? targetRoot = null;
        string? existingBackupPath = null;

        try
        {
            Directory.CreateDirectory(extractRoot);

            ZipValidationResult zipValidation = ValidateAndExtractArchive(zipPath, extractRoot);
            if (!zipValidation.IsSuccess)
            {
                return Fail(zipValidation.Message);
            }

            string? packRoot = ResolvePackRoot(extractRoot);
            if (string.IsNullOrWhiteSpace(packRoot))
            {
                return Fail("ZIP 中未找到有效卡包根目录，必须包含 pack.json。");
            }

            PackManifestLoadResult loadResult = _manifestService.LoadFromDirectory(packRoot);
            if (!loadResult.IsSuccess || loadResult.Manifest is null)
            {
                return Fail(BuildIssueMessage(loadResult.Issues, "卡包协议校验失败。"), loadResult.Issues);
            }

            PackManifest manifest = loadResult.Manifest;
            importedPackId = manifest.PackId;

            string importedRoot = _registryService.GetImportedPacksRoot();
            Directory.CreateDirectory(importedRoot);
            targetRoot = Path.Combine(importedRoot, manifest.PackId);

            ZipPackImportResult preflightResult = ValidateVersionPolicy(manifest, targetRoot);
            if (!preflightResult.IsSuccess)
            {
                return preflightResult;
            }

            WriteImportingState(manifest, targetRoot);

            // 这里先把旧目录移到备份位，再切换新目录。
            // 这样一旦移动新包失败，还能把旧包原样回滚，避免出现“目录被清空但新包没落稳”的半成功状态。
            if (Directory.Exists(targetRoot))
            {
                Directory.CreateDirectory(backupRoot);
                existingBackupPath = Path.Combine(backupRoot, manifest.PackId);
                Directory.Move(targetRoot, existingBackupPath);
            }

            Directory.Move(packRoot, targetRoot);

            InstalledPackState installedState = new()
            {
                PackId = manifest.PackId,
                SourceType = PackSourceType.Imported,
                InstallRoot = targetRoot,
                Status = PackInstanceStatus.Ready,
                LastScanTimeUtc = DateTime.UtcNow,
                LastIndexTimeUtc = DateTime.UtcNow,
                CacheVersion = 1,
                IsDegraded = loadResult.IsDegraded,
                WarningMessage = BuildWarningMessage(loadResult.Issues)
            };
            _installedStateService.Upsert(installedState);

            CleanupDirectory(existingBackupPath);
            CleanupDirectory(extractRoot);

            return new ZipPackImportResult
            {
                IsSuccess = true,
                PackId = manifest.PackId,
                DisplayName = manifest.DisplayName,
                Version = manifest.Version,
                Action = preflightResult.Action,
                Message = BuildSuccessMessage(manifest, preflightResult.Action, loadResult.IsDegraded, installedState.WarningMessage),
                Issues = loadResult.Issues
            };
        }
        catch (Exception ex)
        {
            // ZIP 导入首版必须保证失败可回滚。
            // 这里统一把已切走的旧包恢复回去，并清掉未完成的 installed state，避免注册中心读到不一致实例。
            TryRollbackImportedPack(targetRoot, existingBackupPath);

            if (!string.IsNullOrWhiteSpace(importedPackId) && !string.IsNullOrWhiteSpace(targetRoot))
            {
                _installedStateService.Remove(importedPackId, PackSourceType.Imported, targetRoot);
            }

            return Fail($"导入失败：{ex.GetBaseException().Message}");
        }
        finally
        {
            CleanupDirectory(extractRoot);
            CleanupDirectory(backupRoot);
        }
    }

    private ZipPackImportResult ValidateVersionPolicy(PackManifest manifest, string targetRoot)
    {
        IReadOnlyList<PackDescriptor> packs = _registryService.ScanAvailablePacks(includeLooseFolder: true);
        PackDescriptor? existing = packs.FirstOrDefault(item => item.PackId.Equals(manifest.PackId, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return new ZipPackImportResult
            {
                IsSuccess = true,
                Action = PackImportAction.Imported
            };
        }

        int compare = CompareSemVer(manifest.Version, existing.Version);
        if (compare < 0)
        {
            return new ZipPackImportResult
            {
                IsSuccess = false,
                Action = PackImportAction.Rejected,
                PackId = manifest.PackId,
                DisplayName = manifest.DisplayName,
                Version = manifest.Version,
                Message = $"检测到同 packId 的现有版本为 {existing.Version}，当前 ZIP 为更低版本 {manifest.Version}，已按方案默认拒绝降级导入。"
            };
        }

        if (compare > 0)
        {
            return new ZipPackImportResult
            {
                IsSuccess = true,
                Action = PackImportAction.Upgraded
            };
        }

        return new ZipPackImportResult
        {
            IsSuccess = true,
            Action = Directory.Exists(targetRoot) || existing.SourceType == PackSourceType.Imported
                ? PackImportAction.Reinstalled
                : PackImportAction.Imported
        };
    }

    private void WriteImportingState(PackManifest manifest, string targetRoot)
    {
        InstalledPackState state = new()
        {
            PackId = manifest.PackId,
            SourceType = PackSourceType.Imported,
            InstallRoot = targetRoot,
            Status = PackInstanceStatus.Importing,
            LastScanTimeUtc = DateTime.UtcNow,
            LastIndexTimeUtc = DateTime.UtcNow,
            CacheVersion = 1
        };
        _installedStateService.Upsert(state);
    }

    private static ZipValidationResult ValidateAndExtractArchive(string zipPath, string extractRoot)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count == 0)
        {
            return new ZipValidationResult(false, "ZIP 为空，未发现任何可导入内容。");
        }

        if (archive.Entries.Count > MaxArchiveEntryCount)
        {
            return new ZipValidationResult(false, $"ZIP 文件数量过多，当前上限为 {MaxArchiveEntryCount}。");
        }

        HashSet<string> normalizedEntries = new(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string normalizedName = entry.FullName.Replace('\\', '/');
            if (!IsSafeArchiveEntry(normalizedName))
            {
                return new ZipValidationResult(false, $"ZIP 含有非法路径：{entry.FullName}");
            }

            if (!normalizedEntries.Add(normalizedName))
            {
                return new ZipValidationResult(false, $"ZIP 内存在重复路径：{entry.FullName}");
            }

            if (normalizedName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return new ZipValidationResult(false, $"ZIP 内不允许再嵌套 ZIP：{entry.FullName}");
            }

            totalBytes += entry.Length;
            if (totalBytes > MaxArchiveBytes)
            {
                return new ZipValidationResult(false, $"ZIP 解压总量超过限制，当前上限为 {MaxArchiveBytes / 1024 / 1024} MB。");
            }

            ExtractEntry(entry, extractRoot, normalizedName);
        }

        return new ZipValidationResult(true, string.Empty);
    }

    private static void ExtractEntry(ZipArchiveEntry entry, string extractRoot, string normalizedName)
    {
        string destinationPath = Path.Combine(extractRoot, normalizedName.Replace('/', Path.DirectorySeparatorChar));
        if (normalizedName.EndsWith("/", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(destinationPath);
            return;
        }

        string? destinationDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        using Stream input = entry.Open();
        using FileStream output = File.Create(destinationPath);
        input.CopyTo(output);
    }

    private static string? ResolvePackRoot(string extractRoot)
    {
        string directManifest = Path.Combine(extractRoot, "pack.json");
        if (File.Exists(directManifest))
        {
            return extractRoot;
        }

        List<string> candidates = Directory.GetDirectories(extractRoot)
            .Where(directory => File.Exists(Path.Combine(directory, "pack.json")))
            .ToList();

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static bool IsSafeArchiveEntry(string entryPath)
    {
        if (string.IsNullOrWhiteSpace(entryPath) || Path.IsPathRooted(entryPath))
        {
            return false;
        }

        return !entryPath.StartsWith("/", StringComparison.Ordinal) &&
               !entryPath.Contains("../", StringComparison.Ordinal) &&
               !entryPath.Contains("..\\", StringComparison.Ordinal) &&
               !entryPath.Contains(':', StringComparison.Ordinal);
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

    private static string BuildIssueMessage(IReadOnlyList<PackValidationIssue> issues, string fallback)
    {
        if (issues.Count == 0)
        {
            return fallback;
        }

        return string.Join("\n", issues.Select(issue => issue.Message));
    }

    private static string BuildWarningMessage(IEnumerable<PackValidationIssue> issues)
    {
        return string.Join(" | ", issues.Select(issue => issue.Message).Where(message => !string.IsNullOrWhiteSpace(message)));
    }

    private static string BuildSuccessMessage(PackManifest manifest, PackImportAction action, bool isDegraded, string warningMessage)
    {
        string actionText = action switch
        {
            PackImportAction.Upgraded => "升级导入成功",
            PackImportAction.Reinstalled => "重装成功",
            _ => "导入成功"
        };

        string degradedText = isDegraded ? "；映射部分已降级为素材包浏览模式" : string.Empty;
        string warningText = string.IsNullOrWhiteSpace(warningMessage) ? string.Empty : $"；警告：{warningMessage}";
        return $"{actionText}：{manifest.DisplayName} ({manifest.Version}){degradedText}{warningText}";
    }

    private static void TryRollbackImportedPack(string? targetRoot, string? existingBackupPath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(targetRoot) && Directory.Exists(targetRoot))
            {
                Directory.Delete(targetRoot, true);
            }

            if (!string.IsNullOrWhiteSpace(targetRoot) &&
                !string.IsNullOrWhiteSpace(existingBackupPath) &&
                Directory.Exists(existingBackupPath))
            {
                string? targetParent = Path.GetDirectoryName(targetRoot);
                if (!string.IsNullOrWhiteSpace(targetParent))
                {
                    Directory.CreateDirectory(targetParent);
                }

                Directory.Move(existingBackupPath, targetRoot);
            }
        }
        catch
        {
            // 回滚阶段只尽量恢复，不再抛出次级异常覆盖主错误。
        }
    }

    private static void CleanupDirectory(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(directoryPath, true);
        }
        catch
        {
            // 清理失败不影响主流程，下次导入时可以继续复用工作目录。
        }
    }

    private static ZipPackImportResult Fail(string message, List<PackValidationIssue>? issues = null)
    {
        return new ZipPackImportResult
        {
            IsSuccess = false,
            Action = PackImportAction.Rejected,
            Message = message,
            Issues = issues ?? []
        };
    }

    private readonly record struct ZipValidationResult(bool IsSuccess, string Message);
}
