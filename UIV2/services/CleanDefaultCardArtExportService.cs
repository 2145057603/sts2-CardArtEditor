using Godot;
using System;
using System.IO;
using System.Text;

namespace InfectionCardReplacer.UIV2.Services;

internal sealed class CleanDefaultCardArtExportService
{
    public CleanDefaultCardArtExportResult ExportAllCardPortraits(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Fail("导出目录无效。");
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);

            int exportedCount = 0;
            foreach (CardCatalogRegistry.Entry entry in CardCatalogRegistry.GetEntries())
            {
                if (CardCatalogRegistry.TryGetModel(entry.FullTypeName) is not { Portrait: Texture2D portraitTexture })
                {
                    continue;
                }

                Image? image = portraitTexture.GetImage();
                if (image is null)
                {
                    continue;
                }

                string roleFolder = SanitizePathSegment(string.IsNullOrWhiteSpace(entry.RoleName) ? "unknown" : entry.RoleName);
                string fileName = $"{SanitizePathSegment(entry.TypeName)}.png";
                string outputDirectory = Path.Combine(targetDirectory, roleFolder);
                string outputPath = Path.Combine(outputDirectory, fileName);

                Directory.CreateDirectory(outputDirectory);

                // 这里统一导出为 png，便于后续制作素材包或再次编辑。
                Error saveResult = image.SavePng(outputPath);
                if (saveResult == Error.Ok)
                {
                    exportedCount++;
                }
            }

            if (exportedCount == 0)
            {
                return Fail("没有导出到任何默认卡图，请先确保卡牌目录已经完成加载。");
            }

            WriteReadme(targetDirectory, exportedCount);
            return new CleanDefaultCardArtExportResult
            {
                IsSuccess = true,
                ExportedCount = exportedCount,
                Message = $"已导出 {exportedCount} 张默认卡图到：{targetDirectory}"
            };
        }
        catch (Exception ex)
        {
            return Fail($"导出默认卡图失败：{ex.GetBaseException().Message}");
        }
    }

    private static void WriteReadme(string targetDirectory, int exportedCount)
    {
        string readmePath = Path.Combine(targetDirectory, "README_DefaultCardArt.txt");
        StringBuilder builder = new();
        builder.AppendLine("这是从当前游戏卡牌目录导出的默认卡图。");
        builder.AppendLine("文件按角色目录分组，文件名默认使用卡牌类型名。");
        builder.AppendLine($"导出数量：{exportedCount}");
        File.WriteAllText(readmePath, builder.ToString(), Encoding.UTF8);
    }

    private static string SanitizePathSegment(string text)
    {
        StringBuilder builder = new();
        foreach (char ch in text.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.')
            {
                builder.Append(char.ToLowerInvariant(ch));
                continue;
            }

            if (builder.Length == 0 || builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        string value = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }

    private static CleanDefaultCardArtExportResult Fail(string message)
    {
        return new CleanDefaultCardArtExportResult
        {
            IsSuccess = false,
            Message = message
        };
    }
}

internal sealed class CleanDefaultCardArtExportResult
{
    public bool IsSuccess { get; set; }
    public int ExportedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
