using System.Reflection;
using System.Text.Json;
using Godot;

namespace InfectionCardReplacer;

internal static class CardArtEditorSettings
{
    private sealed class SettingsData
    {
        public string ToggleKey { get; set; } = "F7";
        public bool ShowFullUi { get; set; } = true;
    }

    private static bool _loaded;
    private static string _toggleKeyText = "F7";
    private static Key _toggleKey = Key.F7;
    private static bool _showFullUi = true;

    internal static Key ToggleKey
    {
        get
        {
            EnsureLoaded();
            return _toggleKey;
        }
    }

    internal static string ToggleKeyText
    {
        get
        {
            EnsureLoaded();
            return _toggleKeyText;
        }
    }

    internal static bool ShowFullUi
    {
        get
        {
            EnsureLoaded();
            return _showFullUi;
        }
    }

    internal static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        string path = GetConfigPath();
        if (!File.Exists(path))
        {
            Save();
            return;
        }

        try
        {
            SettingsData? data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(path));
            if (data is null)
            {
                return;
            }

            ApplyToggleKey(data.ToggleKey);
            _showFullUi = data.ShowFullUi;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[InfectionCardReplacer] Failed to load UI settings: {ex.Message}");
        }
    }

    internal static bool TryUpdateToggleKey(string keyText, out string message)
    {
        EnsureLoaded();
        if (!ApplyToggleKey(keyText))
        {
            message = "按键无效。请输入如 F7、F8、K 这样的按键名。";
            return false;
        }

        Save();
        message = $"已将默认打开按键改为 {_toggleKeyText}。";
        return true;
    }

    internal static void UpdateShowFullUi(bool show)
    {
        EnsureLoaded();
        _showFullUi = show;
        Save();
    }

    private static bool ApplyToggleKey(string? keyText)
    {
        string normalized = (keyText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (!Enum.TryParse(normalized, true, out Key parsedKey))
        {
            return false;
        }

        _toggleKey = parsedKey;
        _toggleKeyText = parsedKey.ToString().ToUpperInvariant();
        return true;
    }

    private static void Save()
    {
        try
        {
            SettingsData data = new()
            {
                ToggleKey = _toggleKeyText,
                ShowFullUi = _showFullUi
            };

            JsonSerializerOptions options = new()
            {
                WriteIndented = true
            };

            File.WriteAllText(GetConfigPath(), JsonSerializer.Serialize(data, options));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[InfectionCardReplacer] Failed to save UI settings: {ex.Message}");
        }
    }

    private static string GetConfigPath()
    {
        string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return Path.Combine(assemblyDir ?? ".", "card_art_editor_settings.json");
    }
}
