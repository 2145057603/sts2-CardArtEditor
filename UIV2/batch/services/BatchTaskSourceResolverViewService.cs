using InfectionCardReplacer.PackSystem.Models;
using InfectionCardReplacer.PackSystem.Services;
using InfectionCardReplacer.UIV2.Batch.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InfectionCardReplacer.UIV2.Batch.Services;

internal sealed class BatchTaskSourceResolverViewService
{
    private readonly PackRegistryService _registryService = new();
    private readonly PackIndexBuilder _indexBuilder = new();
    private readonly UserPackStateService _userStateService = new();

    public List<BatchTaskSourceOption> GetSourceOptions()
    {
        return GetEnabledDescriptors()
            .Select(descriptor => new BatchTaskSourceOption
            {
                PackId = descriptor.PackId,
                DisplayName = BuildSourceDisplayName(descriptor)
            })
            .ToList();
    }

    public List<BatchTaskPackSource> ResolveSources(BatchTaskSourceMode sourceMode, string selectedPackId)
    {
        IEnumerable<PackDescriptor> descriptors = GetEnabledDescriptors();
        descriptors = sourceMode switch
        {
            BatchTaskSourceMode.SinglePack => descriptors.Where(item =>
                item.PackId.Equals(selectedPackId, StringComparison.OrdinalIgnoreCase)),
            BatchTaskSourceMode.ReleasePacksOnly => descriptors.Where(ReleasePackDescriptorHelper.IsReleasePack),
            _ => descriptors
        };

        List<BatchTaskPackSource> resolved = [];
        foreach (PackDescriptor descriptor in descriptors)
        {
            PackIndex? index = _indexBuilder.BuildIndex(descriptor);
            if (index is null)
            {
                continue;
            }

            resolved.Add(new BatchTaskPackSource
            {
                Descriptor = descriptor,
                Index = index
            });
        }

        return resolved;
    }

    private List<PackDescriptor> GetEnabledDescriptors()
    {
        return _registryService.ScanAvailablePacks(includeLooseFolder: true)
            .Where(descriptor => _userStateService.IsEnabledForSelection(descriptor.PackId))
            .OrderByDescending(descriptor => descriptor.Priority)
            .ThenByDescending(descriptor => GetSourceRank(descriptor.SourceType))
            .ThenBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GetSourceRank(PackSourceType sourceType)
    {
        return sourceType switch
        {
            PackSourceType.Imported => 3,
            PackSourceType.LooseFolder => 2,
            _ => 1
        };
    }

    private static string BuildSourceDisplayName(PackDescriptor descriptor)
    {
        string releaseSuffix = ReleasePackDescriptorHelper.IsReleasePack(descriptor) ? " [发布包]" : string.Empty;
        return $"{descriptor.DisplayName}{releaseSuffix} ({descriptor.SourceType})";
    }
}
