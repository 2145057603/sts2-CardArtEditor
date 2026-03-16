using InfectionCardReplacer.PackSystem.Models;
using Godot;
using System.IO.Compression;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class PackExportService
{
    private static readonly string[] SupportedExtensions =
    [
        ".zip",
        ".cardartpack",
        ".sts2cardpack"
    ];

    public PackExportResult ExportPackArchive(PackDescriptor? descriptor, string destinationPath)
    {
        if (descriptor is null)
        {
            return Fail("当前没有可导出的卡包。");
        }

        if (string.IsNullOrWhiteSpace(descriptor.RootPath) || !Directory.Exists(descriptor.RootPath))
        {
            return Fail($"卡包目录不存在：{descriptor.RootPath}");
        }

        string resolvedPath = NormalizeDestinationPath(destinationPath);
        string extension = Path.GetExtension(resolvedPath);
        if (!IsSupportedExtension(extension))
        {
            return Fail("导出格式仅支持 .zip / .cardartpack / .sts2cardpack");
        }

        string stagingRoot = Path.Combine(
            ProjectSettings.GlobalizePath("user://pack_work"),
            "export",
            Guid.NewGuid().ToString("N"));
        string stagingPackRoot = Path.Combine(stagingRoot, descriptor.PackId);

        try
        {
            Directory.CreateDirectory(stagingPackRoot);

            // 先复制到临时目录再打包，避免直接碰用户现有卡包目录。
            CopyDirectory(descriptor.RootPath, stagingPackRoot);
            WriteManifestAliases(stagingPackRoot);

            string? targetDirectory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(resolvedPath))
            {
                File.Delete(resolvedPath);
            }

            // 这里把 packId 作为压缩包顶层目录，导入时更稳，也避免解压后文件直接散落。
            ZipFile.CreateFromDirectory(stagingRoot, resolvedPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            return new PackExportResult
            {
                IsSuccess = true,
                OutputPath = resolvedPath,
                Message = $"已导出卡包：{Path.GetFileName(resolvedPath)}"
            };
        }
        catch (Exception ex)
        {
            return Fail($"导出卡包失败：{ex.GetBaseException().Message}");
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    public static string BuildSuggestedFileName(PackDescriptor descriptor, string extension)
    {
        string safePackId = string.IsNullOrWhiteSpace(descriptor.PackId) ? "card-pack" : descriptor.PackId;
        string safeVersion = string.IsNullOrWhiteSpace(descriptor.Version) ? "1.0.0" : descriptor.Version;
        return $"{safePackId}-{safeVersion}{extension}";
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

    private static bool IsSupportedExtension(string extension)
    {
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (string filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            string targetPath = Path.Combine(targetDirectory, relativePath);
            string? targetParent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetParent))
            {
                Directory.CreateDirectory(targetParent);
            }

            File.Copy(filePath, targetPath, overwrite: true);
        }
    }

    private static void WriteManifestAliases(string packRoot)
    {
        string standardManifestPath = Path.Combine(packRoot, "pack.json");
        if (!File.Exists(standardManifestPath))
        {
            return;
        }

        string manifestContent = File.ReadAllText(standardManifestPath);

        // 除标准 pack.json 外，再额外导出两个未来可扩展的协议别名，
        // 方便后续卡面生态接统一包格式时直接识别。
        File.WriteAllText(Path.Combine(packRoot, "cardartpack.json"), manifestContent);
        File.WriteAllText(Path.Combine(packRoot, "sts2cardpack.json"), manifestContent);
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(directoryPath, recursive: true);
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

internal sealed class PackExportResult
{
    public bool IsSuccess { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
