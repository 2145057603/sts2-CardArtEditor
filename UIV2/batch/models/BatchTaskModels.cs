using InfectionCardReplacer.PackSystem.Models;

namespace InfectionCardReplacer.UIV2.Batch.Models;

internal enum BatchTaskScope
{
    CurrentFiltered,
    CurrentRole,
    AllCards
}

internal enum BatchTaskSourceMode
{
    AllEnabledPacks,
    SinglePack,
    ReleasePacksOnly
}

internal enum BatchTaskMatchMode
{
    ExplicitAndFileName,
    ExplicitOnly,
    FileNameOnly
}

internal enum BatchTaskEntryStatus
{
    Matched,
    Conflict,
    Missing,
    Manual
}

internal sealed class BatchTaskGenerateOptions
{
    public string TaskName { get; set; } = string.Empty;
    public BatchTaskScope Scope { get; set; } = BatchTaskScope.CurrentFiltered;
    public BatchTaskSourceMode SourceMode { get; set; } = BatchTaskSourceMode.AllEnabledPacks;
    public string SelectedPackId { get; set; } = string.Empty;
    public BatchTaskMatchMode MatchMode { get; set; } = BatchTaskMatchMode.ExplicitAndFileName;
}

internal sealed class BatchTaskSourceOption
{
    public string PackId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

internal sealed class BatchTaskPackSource
{
    public PackDescriptor Descriptor { get; set; } = new();
    public PackIndex Index { get; set; } = new();
}

internal sealed class BatchTaskEntry
{
    public string CardId { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string CardType { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string SourcePackId { get; set; } = string.Empty;
    public string SourcePackName { get; set; } = string.Empty;
    public string CandidateImagePath { get; set; } = string.Empty;
    public string CandidateRelativePath { get; set; } = string.Empty;
    public string MatchReason { get; set; } = string.Empty;
    public BatchTaskEntryStatus Status { get; set; } = BatchTaskEntryStatus.Missing;
    public int ConflictCount { get; set; }
    public bool IsLocked { get; set; }
    public bool IsSkipped { get; set; }
}

internal sealed class BatchTaskSummary
{
    public int Total { get; set; }
    public int Matched { get; set; }
    public int Missing { get; set; }
    public int Conflict { get; set; }
    public int Manual { get; set; }
    public int Locked { get; set; }
    public int Skipped { get; set; }
}
