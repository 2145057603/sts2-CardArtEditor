using InfectionCardReplacer.PackSystem.Models;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class ReleasePackApplyService
{
    private readonly PackManifestService _manifestService = new();

    public ReleasePackApplyResult ApplyImportedReleasePack(PackDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return Fail("未找到刚导入的发布包。");
        }

        if (string.IsNullOrWhiteSpace(descriptor.RootPath) || !Directory.Exists(descriptor.RootPath))
        {
            return Fail("发布包目录不存在，无法应用。");
        }

        PackManifestLoadResult loadResult = _manifestService.LoadFromDirectory(descriptor.RootPath);
        if (!loadResult.IsSuccess || loadResult.Manifest is null || loadResult.MappingFile is null)
        {
            return Fail("发布包映射读取失败，无法自动应用。");
        }

        int appliedCount = 0;
        foreach (PackMappingEntry mapping in loadResult.MappingFile.Entries)
        {
            if (string.IsNullOrWhiteSpace(mapping.CardId) || string.IsNullOrWhiteSpace(mapping.Image))
            {
                continue;
            }

            string absoluteImagePath = Path.Combine(descriptor.RootPath, mapping.Image.Replace("/", Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal));
            if (!File.Exists(absoluteImagePath))
            {
                continue;
            }

            // 发布包导入后要直接落到当前替换结果里，这里按 cardId 一条条覆盖。
            CardArtOverrideRegistry.SetOverride(mapping.CardId, absoluteImagePath);
            appliedCount++;
        }

        if (appliedCount == 0)
        {
            return Fail("发布包中没有可应用的映射图片。");
        }

        return new ReleasePackApplyResult
        {
            IsSuccess = true,
            AppliedCount = appliedCount,
            Message = $"发布包已导入并应用 {appliedCount} 张卡牌。"
        };
    }

    private static ReleasePackApplyResult Fail(string message)
    {
        return new ReleasePackApplyResult
        {
            IsSuccess = false,
            Message = message
        };
    }
}

internal sealed class ReleasePackApplyResult
{
    public bool IsSuccess { get; set; }
    public int AppliedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
