using InfectionCardReplacer.PackSystem.Models;
using InfectionCardReplacer.UIV2.Batch.Models;
using System.IO;

namespace InfectionCardReplacer.UIV2.Batch.Services;

internal sealed class BatchTaskManualCandidateService
{
    public BatchTaskManualCandidateResult TryAssignToEmptyEntry(
        BatchTaskEntry? entry,
        PackEntry? sourceEntry,
        string sourcePackName)
    {
        if (entry is null)
        {
            return BatchTaskManualCandidateResult.Fail("请先在中间表格选择一条批量任务。");
        }

        if (sourceEntry is null)
        {
            return BatchTaskManualCandidateResult.Fail("当前来源图片无效。");
        }

        if (entry.IsLocked)
        {
            return BatchTaskManualCandidateResult.Fail("当前条目已锁定，不能直接修改候选图。");
        }

        if (!string.IsNullOrWhiteSpace(entry.CandidateImagePath))
        {
            return BatchTaskManualCandidateResult.Fail("当前条目已有候选图，先清空候选后再手动指定。");
        }

        if (string.IsNullOrWhiteSpace(sourceEntry.AbsolutePath) || !File.Exists(sourceEntry.AbsolutePath))
        {
            return BatchTaskManualCandidateResult.Fail("所选来源图片文件不存在。");
        }

        // 这里只在空候选时写入，避免手动点图时误覆盖系统已经给出的结果。
        entry.CandidateImagePath = sourceEntry.AbsolutePath;
        entry.CandidateRelativePath = sourceEntry.RelativePath;
        entry.SourcePackId = sourceEntry.PackId;
        entry.SourcePackName = string.IsNullOrWhiteSpace(sourcePackName) ? sourceEntry.PackId : sourcePackName;
        entry.MatchReason = "手动选择";
        entry.Status = BatchTaskEntryStatus.Manual;
        entry.ConflictCount = 0;

        return BatchTaskManualCandidateResult.Success($"已为 {entry.CardName} 手动指定候选图。");
    }
}

internal sealed class BatchTaskManualCandidateResult
{
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; } = string.Empty;

    public static BatchTaskManualCandidateResult Success(string message)
    {
        return new BatchTaskManualCandidateResult
        {
            IsSuccess = true,
            Message = message
        };
    }

    public static BatchTaskManualCandidateResult Fail(string message)
    {
        return new BatchTaskManualCandidateResult
        {
            IsSuccess = false,
            Message = message
        };
    }
}
