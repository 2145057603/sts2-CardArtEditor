namespace InfectionCardReplacer.UIV2.Services;

internal sealed class ConfigService
{
    public string GetToggleKeyText()
    {
        ShortcutSettingsService.EnsureLoaded();
        return ShortcutSettingsService.ToggleShortcutText;
    }

    public string GetResetShortcutText()
    {
        ShortcutSettingsService.EnsureLoaded();
        return ShortcutSettingsService.ResetShortcutText;
    }

    public string GetPackBrowserShortcutText()
    {
        ShortcutSettingsService.EnsureLoaded();
        return ShortcutSettingsService.PackBrowserShortcutText;
    }

    public string GetPickerShortcutText()
    {
        ShortcutSettingsService.EnsureLoaded();
        return ShortcutSettingsService.PickerShortcutText;
    }

    public bool GetShowFullUi()
    {
        CardArtEditorSettings.EnsureLoaded();
        return CardArtEditorSettings.ShowFullUi;
    }
}
