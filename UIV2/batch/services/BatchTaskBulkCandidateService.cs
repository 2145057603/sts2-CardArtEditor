using InfectionCardReplacer.UIV2.Batch.Models;
using System.Collections.Generic;

namespace InfectionCardReplacer.UIV2.Batch.Services;

internal sealed class BatchTaskBulkCandidateService
{
    public BatchTaskBulkCandidateClearResult ClearAllCandidates(IReadOnlyList<BatchTaskEntry> entries)
    {
        int cleared = 0;
        int skippedLocked = 0;

        foreach (BatchTaskEntry entry in entries)
        {
            if (entry.IsLocked)
            {
                skippedLocked++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.CandidateImagePath) &&
                string.IsNullOrWhiteSpace(entry.CandidateRelativePath) &&
                string.IsNullOrWhiteSpace(entry.SourcePackId) &&
                string.IsNullOrWhiteSpace(entry.SourcePackName))
            {
                continue;
            }

            // 批量清空时统一回到“未匹配”状态，方便后续重新选择候选图。
            entry.CandidateImagePath = string.Empty;
            entry.CandidateRelativePath = string.Empty;
            entry.SourcePackId = string.Empty;
            entry.SourcePackName = string.Empty;
            entry.MatchReason = "已清空";
            entry.Status = BatchTaskEntryStatus.Missing;
            entry.ConflictCount = 0;
            cleared++;
        }

        string message = skippedLocked > 0
            ? $"已清空 {cleared} 条候选，保留 {skippedLocked} 条已锁定项。"
            : $"已清空 {cleared} 条候选。";

        return new BatchTaskBulkCandidateClearResult
        {
            ClearedCount = cleared,
            SkippedLockedCount = skippedLocked,
            Message = message
        };
    }
}

internal sealed class BatchTaskBulkCandidateClearResult
{
    public int ClearedCount { get; set; }
    public int SkippedLockedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
