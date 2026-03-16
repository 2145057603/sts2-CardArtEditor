using InfectionCardReplacer.UIV2.Batch.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace InfectionCardReplacer.UIV2.Batch.Services;

internal sealed class BatchTaskApplyService
{
    public BatchTaskApplyResult ApplyConfirmedEntries(IEnumerable<BatchTaskEntry> entries)
    {
        int applied = 0;
        int skipped = 0;
        int failed = 0;

        foreach (BatchTaskEntry entry in entries)
        {
            if (entry.IsSkipped)
            {
                skipped++;
                continue;
            }

            if (entry.Status is not (BatchTaskEntryStatus.Matched or BatchTaskEntryStatus.Manual))
            {
                skipped++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.CandidateImagePath) || !File.Exists(entry.CandidateImagePath))
            {
                failed++;
                continue;
            }

            string result = CardArtOverrideRegistry.SetOverride(entry.CardId, entry.CandidateImagePath);
            if (result.Contains("失败", StringComparison.OrdinalIgnoreCase))
            {
                failed++;
                continue;
            }

            applied++;
        }

        return new BatchTaskApplyResult
        {
            AppliedCount = applied,
            SkippedCount = skipped,
            FailedCount = failed,
            Message = $"批量应用完成：成功 {applied}，跳过 {skipped}，失败 {failed}"
        };
    }
}

internal sealed class BatchTaskApplyResult
{
    public int AppliedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
