using System.Text.Json.Serialization;

namespace InfectionCardReplacer.PackSystem.Models;

internal enum PackType
{
    AssetPack,
    MappingPack,
    HybridPack
}

internal enum PackSourceType
{
    Bundled,
    Imported,
    LooseFolder
}

internal enum PackInstanceStatus
{
    Importing,
    Ready,
    Invalid,
    Broken,
    Disabled
}

internal sealed class PackManifest
{
    public int FormatVersion { get; set; }
    public string PackId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter<PackType>))]
    public PackType PackType { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string EntryRoot { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string License { get; set; } = string.Empty;
    public string Homepage { get; set; } = string.Empty;
    public int Priority { get; set; } = 100;
    public string MappingsFile { get; set; } = string.Empty;
}

internal sealed class PackMappingEntry
{
    public string CardId { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

internal sealed class PackMappingFile
{
    public List<PackMappingEntry> Entries { get; set; } = [];
}

internal sealed class PackValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

internal sealed class PackManifestLoadResult
{
    public PackManifest? Manifest { get; set; }
    public PackMappingFile? MappingFile { get; set; }
    public List<PackValidationIssue> Issues { get; } = [];
    public bool IsSuccess { get; set; }
    public bool IsDegraded { get; set; }
}

internal sealed class PackDescriptor
{
    public string PackId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter<PackType>))]
    public PackType PackType { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<PackSourceType>))]
    public PackSourceType SourceType { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public string ManifestPath { get; set; } = string.Empty;
    public string CoverPath { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; } = 100;
    public List<string> Tags { get; set; } = [];
    public bool IsDegraded { get; set; }
}

internal sealed class PackEntry
{
    public string EntryId { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public string AbsolutePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Kind { get; set; } = "image";
    public int Width { get; set; }
    public int Height { get; set; }
    public string ThumbnailCacheKey { get; set; } = string.Empty;
}

internal sealed class PackIndex
{
    public PackDescriptor Descriptor { get; set; } = new();
    public List<PackEntry> Entries { get; set; } = [];
    public Dictionary<string, string> DefaultMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class UserPackState
{
    public string PackId { get; set; } = string.Empty;
    public string UserNote { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public List<string> UserTags { get; set; } = [];
    public int? UserPriorityOverride { get; set; }
    public bool EnabledForSelection { get; set; } = true;
}

internal sealed class PackEntryUserState
{
    public string EntryKey { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string UserNote { get; set; } = string.Empty;
}

internal sealed class InstalledPackState
{
    public string InstanceId { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter<PackSourceType>))]
    public PackSourceType SourceType { get; set; }
    public string InstallRoot { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter<PackInstanceStatus>))]
    public PackInstanceStatus Status { get; set; } = PackInstanceStatus.Ready;
    public DateTime LastScanTimeUtc { get; set; }
    public DateTime LastIndexTimeUtc { get; set; }
    public int CacheVersion { get; set; }
    public bool IsDegraded { get; set; }
    public string WarningMessage { get; set; } = string.Empty;
}

internal sealed class BundledPackRegistry
{
    public List<string> Packs { get; set; } = [];
}

internal enum PackImportAction
{
    Imported,
    Upgraded,
    Reinstalled,
    Rejected
}

internal sealed class ZipPackImportResult
{
    public bool IsSuccess { get; set; }
    public string PackId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public PackImportAction Action { get; set; } = PackImportAction.Imported;
    public string Message { get; set; } = string.Empty;
    public List<PackValidationIssue> Issues { get; set; } = [];
}

internal sealed class PackDeleteResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}
