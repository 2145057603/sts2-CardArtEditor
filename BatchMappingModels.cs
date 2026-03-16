using System.Text.Json.Serialization;

namespace InfectionCardReplacer;

internal enum BatchEntryStatus
{
    Unmatched,
    Matched,
    Manual,
    Conflict
}

internal enum BatchMatchSource
{
    AutoMatch,
    Sequence,
    ManualAssign,
    PresetLoad,
    PackApply
}

internal enum BatchAssetSourceType
{
    LooseImages,
    BuiltInPack,
    ExternalPack,
    SavedPreset
}

internal enum BatchSessionMode
{
    AutoMapping,
    ManualSequence,
    PresetReplay,
    PackApplication
}

internal sealed class BatchEntry
{
    public string CardId { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string CardType { get; set; } = string.Empty;
    public string CardRarity { get; set; } = string.Empty;
    public string CardColor { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string TargetImagePath { get; set; } = string.Empty;
    public string TargetImageName { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter<BatchEntryStatus>))]
    public BatchEntryStatus Status { get; set; } = BatchEntryStatus.Unmatched;
    [JsonConverter(typeof(JsonStringEnumConverter<BatchMatchSource>))]
    public BatchMatchSource MatchSource { get; set; } = BatchMatchSource.AutoMatch;
    [JsonConverter(typeof(JsonStringEnumConverter<BatchAssetSourceType>))]
    public BatchAssetSourceType AssetSourceType { get; set; } = BatchAssetSourceType.LooseImages;
    public string AssetSourceId { get; set; } = string.Empty;
}

internal sealed class BatchSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public List<string> SelectedCards { get; set; } = [];
    public List<string> ImportedImages { get; set; } = [];
    public List<BatchEntry> Entries { get; set; } = [];
    [JsonConverter(typeof(JsonStringEnumConverter<BatchSessionMode>))]
    public BatchSessionMode Mode { get; set; } = BatchSessionMode.AutoMapping;
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedTime { get; set; } = DateTime.UtcNow;

    public void Touch()
    {
        LastModifiedTime = DateTime.UtcNow;
    }
}
