using InfectionCardReplacer.PackSystem.Models;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class PackMaintenanceService
{
    private readonly InstalledPackStateService _installedStateService = new();

    public PackDeleteResult DeleteImportedPack(PackDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return new PackDeleteResult
            {
                IsSuccess = false,
                Message = "当前没有可删除的卡包。"
            };
        }

        if (descriptor.SourceType != PackSourceType.Imported)
        {
            return new PackDeleteResult
            {
                IsSuccess = false,
                Message = "只有 Imported 卡包允许在管理界面中物理删除。"
            };
        }

        if (string.IsNullOrWhiteSpace(descriptor.RootPath) || !Directory.Exists(descriptor.RootPath))
        {
            _installedStateService.Remove(descriptor.PackId, descriptor.SourceType, descriptor.RootPath);
            return new PackDeleteResult
            {
                IsSuccess = true,
                Message = $"卡包 {descriptor.DisplayName} 的目录已不存在，已清理安装状态。"
            };
        }

        try
        {
            // 这里只删除 Imported 的安装目录，不动用户备注与图片备注，保证以后重装还能继承。
            Directory.Delete(descriptor.RootPath, true);
            _installedStateService.Remove(descriptor.PackId, descriptor.SourceType, descriptor.RootPath);
            return new PackDeleteResult
            {
                IsSuccess = true,
                Message = $"已删除卡包：{descriptor.DisplayName}"
            };
        }
        catch (Exception ex)
        {
            return new PackDeleteResult
            {
                IsSuccess = false,
                Message = $"删除失败：{ex.GetBaseException().Message}"
            };
        }
    }
}
