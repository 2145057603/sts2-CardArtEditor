namespace InfectionCardReplacer.UIV2.Services;

internal sealed class SkinCenterFacade
{
    public CardRepository Cards { get; } = new();
    public PreviewService Preview { get; } = new();
    public ReplaceService Replace { get; } = new();
    public ConfigService Config { get; } = new();
    public HistoryService History { get; } = new();
}
