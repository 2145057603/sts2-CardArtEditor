using InfectionCardReplacer.PackSystem.Models;
using System;
using System.Linq;

namespace InfectionCardReplacer.PackSystem.Services;

internal static class ReleasePackDescriptorHelper
{
    public static bool IsReleasePack(PackDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return false;
        }

        // 发布包目前以导出标签作为最稳定标记，同时要求它本身是映射包，
        // 这样不会把普通素材包误判成可一键替换的成品包。
        return descriptor.PackType == PackType.MappingPack &&
               descriptor.Tags.Any(tag => tag.Equals("release-export", StringComparison.OrdinalIgnoreCase));
    }
}
