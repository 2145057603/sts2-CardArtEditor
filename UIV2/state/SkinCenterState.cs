using System.Collections.Generic;

namespace InfectionCardReplacer.UIV2.State;

internal sealed class SkinCenterState
{
    public string SearchKeyword { get; set; } = string.Empty;
    public string SelectedCardId { get; set; } = string.Empty;
    public bool IsDetailDrawerOpen { get; set; }
    public bool ShowReplacementPreview { get; set; } = true;
    public string StatusMessage { get; set; } = "等待操作";
    public string PendingImagePath { get; set; } = string.Empty;
    public bool OnlyShowReplaced { get; set; }
    public HashSet<string> SelectedRoles { get; set; } = [];
    public List<SkinCenterCardItem> FilteredCards { get; set; } = [];
}
