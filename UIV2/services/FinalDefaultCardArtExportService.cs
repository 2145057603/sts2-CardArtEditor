using Godot;
using System;
using System.IO;
using System.Text;

namespace InfectionCardReplacer.UIV2.Services;

internal sealed class FinalDefaultCardArtExportService
{
    public FinalDefaultCardArtExportResult ExportAllCardPortraits(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return Fail("导出目录无效。");
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);

            int exportedCount = 0;
            int skippedCount = 0;
            foreach (CardCatalogRegistry.Entry entry in CardCatalogRegistry.GetEntries())
            {
                if (CardCatalogRegistry.TryGetModel(entry.FullTypeName) is not { Portrait: Texture2D portraitTexture })
                {
                    skippedCount++;
                    continue;
                }

                Image? image = ExtractTextureImage(portraitTexture);
                if (image is null || IsMostlyBlack(image))
                {
                    skippedCount++;
                    continue;
                }

                string roleFolder = SanitizePathSegment(string.IsNullOrWhiteSpace(entry.RoleName) ? "unknown" : entry.RoleName);
                string fileName = $"{SanitizePathSegment(entry.TypeName)}.png";
                string outputDirectory = Path.Combine(targetDirectory, roleFolder);
                string outputPath = Path.Combine(outputDirectory, fileName);
                Directory.CreateDirectory(outputDirectory);

                // 图集切片会先裁成独立图像，再统一导出为 png。
                Error saveResult = image.SavePng(outputPath);
                if (saveResult == Error.Ok)
                {
                    exportedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            if (exportedCount == 0)
            {
                return Fail("没有导出到任何有效默认卡图，请先确保卡牌目录已经完成加载。");
            }

            WriteReadme(targetDirectory, exportedCount, skippedCount);
            return new FinalDefaultCardArtExportResult
            {
                IsSuccess = true,
                ExportedCount = exportedCount,
                SkippedCount = skippedCount,
                Message = $"已导出 {exportedCount} 张默认卡图，跳过 {skippedCount} 张无效或黑图资源。"
            };
        }
        catch (Exception ex)
        {
            return Fail($"导出默认卡图失败：{ex.GetBaseException().Message}");
        }
    }

    private static Image? ExtractTextureImage(Texture2D texture)
    {
        if (texture is AtlasTexture atlasTexture)
        {
            return ExtractAtlasTextureImage(atlasTexture);
        }

        try
        {
            return texture.GetImage();
        }
        catch
        {
            return null;
        }
    }

    private static Image? ExtractAtlasTextureImage(AtlasTexture atlasTexture)
    {
        if (atlasTexture.Atlas is null)
        {
            return null;
        }

        Image? atlasImage;
        try
        {
            atlasImage = atlasTexture.Atlas.GetImage();
        }
        catch
        {
            return null;
        }

        if (atlasImage is null)
        {
            return null;
        }

        Rect2 region = atlasTexture.Region;
        Rect2I cropRect = new(
            Mathf.RoundToInt(region.Position.X),
            Mathf.RoundToInt(region.Position.Y),
            Mathf.RoundToInt(region.Size.X),
            Mathf.RoundToInt(region.Size.Y));
        if (cropRect.Size.X <= 0 || cropRect.Size.Y <= 0)
        {
            return null;
        }

        Rect2I atlasBounds = new(0, 0, atlasImage.GetWidth(), atlasImage.GetHeight());
        Rect2I clippedRect = cropRect.Intersection(atlasBounds);
        if (clippedRect.Size.X <= 0 || clippedRect.Size.Y <= 0)
        {
            return null;
        }

        return atlasImage.GetRegion(clippedRect);
    }

    private static bool IsMostlyBlack(Image image)
    {
        int width = image.GetWidth();
        int height = image.GetHeight();
        if (width <= 0 || height <= 0)
        {
            return true;
        }

        int opaquePixels = 0;
        int darkPixels = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = image.GetPixel(x, y);
                if (pixel.A <= 0.01f)
                {
                    continue;
                }

                opaquePixels++;
                float brightness = (pixel.R + pixel.G + pixel.B) / 3.0f;
                if (brightness <= 0.02f)
                {
                    darkPixels++;
                }
            }
        }

        if (opaquePixels == 0)
        {
            return true;
        }

        return darkPixels >= opaquePixels * 0.98f;
    }

    private static void WriteReadme(string targetDirectory, int exportedCount, int skippedCount)
    {
        string readmePath = Path.Combine(targetDirectory, "README_DefaultCardArt.txt");
        StringBuilder builder = new();
        builder.AppendLine("这是从当前游戏卡牌目录导出的默认卡图。");
        builder.AppendLine("文件按角色目录分组，文件名默认使用卡牌类型名。");
        builder.AppendLine("导出时会自动处理图集切片纹理，并跳过明显的黑图资源。");
        builder.AppendLine($"导出数量：{exportedCount}");
        builder.AppendLine($"跳过数量：{skippedCount}");
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

    private static FinalDefaultCardArtExportResult Fail(string message)
    {
        return new FinalDefaultCardArtExportResult
        {
            IsSuccess = false,
            Message = message
        };
    }
}

internal sealed class FinalDefaultCardArtExportResult
{
    public bool IsSuccess { get; set; }
    public int ExportedCount { get; set; }
    public int SkippedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
