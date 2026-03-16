using Godot;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace InfectionCardReplacer.UIV2.Services;

internal static class ShortcutSettingsService
{
    private sealed class SettingsData
    {
        public string ToggleKey { get; set; } = DefaultToggleShortcutText;
        public string ResetShortcut { get; set; } = DefaultResetShortcutText;
        public string PackBrowserShortcut { get; set; } = DefaultPackBrowserShortcutText;
        public string PickerShortcut { get; set; } = DefaultPickerShortcutText;
        public bool ShowFullUi { get; set; } = true;
    }

    private readonly record struct ShortcutBinding(Key Key, bool Ctrl, bool Alt, bool Shift, string DisplayText);

    private const string DefaultToggleShortcutText = "F7";
    private const string DefaultResetShortcutText = "Ctrl+R";
    private const string DefaultPackBrowserShortcutText = "Ctrl+B";
    private const string DefaultPickerShortcutText = "Ctrl+P";

    private static bool _loaded;
    private static ShortcutBinding _toggleShortcut = CreateDefaultBinding(DefaultToggleShortcutText, Key.F7);
    private static ShortcutBinding _resetShortcut = CreateDefaultBinding(DefaultResetShortcutText, Key.R, ctrl: true);
    private static ShortcutBinding _packBrowserShortcut = CreateDefaultBinding(DefaultPackBrowserShortcutText, Key.B, ctrl: true);
    private static ShortcutBinding _pickerShortcut = CreateDefaultBinding(DefaultPickerShortcutText, Key.P, ctrl: true);
    private static bool _showFullUi = true;

    public static string ToggleShortcutText
    {
        get
        {
            EnsureLoaded();
            return _toggleShortcut.DisplayText;
        }
    }

    public static string ResetShortcutText
    {
        get
        {
            EnsureLoaded();
            return _resetShortcut.DisplayText;
        }
    }

    public static string PackBrowserShortcutText
    {
        get
        {
            EnsureLoaded();
            return _packBrowserShortcut.DisplayText;
        }
    }

    public static string PickerShortcutText
    {
        get
        {
            EnsureLoaded();
            return _pickerShortcut.DisplayText;
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        string path = GetConfigPath();
        if (!File.Exists(path))
        {
            // 继续沿用原来的设置文件名，这样旧版 F7 配置不会因为切到 UIV2 而丢失。
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

            ApplyShortcutOrDefault(data.ToggleKey, ref _toggleShortcut, DefaultToggleShortcutText, Key.F7);
            ApplyShortcutOrDefault(data.ResetShortcut, ref _resetShortcut, DefaultResetShortcutText, Key.R, ctrl: true);
            ApplyShortcutOrDefault(data.PackBrowserShortcut, ref _packBrowserShortcut, DefaultPackBrowserShortcutText, Key.B, ctrl: true);
            ApplyShortcutOrDefault(data.PickerShortcut, ref _pickerShortcut, DefaultPickerShortcutText, Key.P, ctrl: true);
            _showFullUi = data.ShowFullUi;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[CardArtEditor][UIV2] Failed to load shortcut settings: {ex.Message}");
        }
    }

    public static bool TryUpdateToggleShortcut(string shortcutText, out string message)
    {
        EnsureLoaded();
        return TryUpdateShortcut(shortcutText, ref _toggleShortcut, "界面开关快捷键", out message);
    }

    public static bool TryUpdateResetShortcut(string shortcutText, out string message)
    {
        EnsureLoaded();
        return TryUpdateShortcut(shortcutText, ref _resetShortcut, "恢复默认快捷键", out message);
    }

    public static bool TryUpdatePackBrowserShortcut(string shortcutText, out string message)
    {
        EnsureLoaded();
        return TryUpdateShortcut(shortcutText, ref _packBrowserShortcut, "卡包浏览快捷键", out message);
    }

    public static bool TryUpdatePickerShortcut(string shortcutText, out string message)
    {
        EnsureLoaded();
        return TryUpdateShortcut(shortcutText, ref _pickerShortcut, "选图栏快捷键", out message);
    }

    public static void ResetShortcutsToDefault()
    {
        EnsureLoaded();
        _toggleShortcut = CreateDefaultBinding(DefaultToggleShortcutText, Key.F7);
        _resetShortcut = CreateDefaultBinding(DefaultResetShortcutText, Key.R, ctrl: true);
        _packBrowserShortcut = CreateDefaultBinding(DefaultPackBrowserShortcutText, Key.B, ctrl: true);
        _pickerShortcut = CreateDefaultBinding(DefaultPickerShortcutText, Key.P, ctrl: true);
        Save();
    }

    public static bool MatchesToggleShortcut(InputEventKey keyEvent)
    {
        EnsureLoaded();
        return MatchesShortcut(keyEvent, _toggleShortcut);
    }

    public static bool MatchesResetShortcut(InputEventKey keyEvent)
    {
        EnsureLoaded();
        return MatchesShortcut(keyEvent, _resetShortcut);
    }

    public static bool MatchesPackBrowserShortcut(InputEventKey keyEvent)
    {
        EnsureLoaded();
        return MatchesShortcut(keyEvent, _packBrowserShortcut);
    }

    public static bool MatchesPickerShortcut(InputEventKey keyEvent)
    {
        EnsureLoaded();
        return MatchesShortcut(keyEvent, _pickerShortcut);
    }

    public static bool TryFormatShortcut(InputEventKey keyEvent, out string shortcutText)
    {
        shortcutText = string.Empty;
        if (!keyEvent.Pressed || keyEvent.Echo || IsModifierKey(keyEvent.Keycode))
        {
            return false;
        }

        shortcutText = BuildShortcutText(keyEvent.Keycode, keyEvent.CtrlPressed, keyEvent.AltPressed, keyEvent.ShiftPressed);
        return true;
    }

    private static bool TryUpdateShortcut(string shortcutText, ref ShortcutBinding binding, string label, out string message)
    {
        if (!TryParseShortcut(shortcutText, out ShortcutBinding parsed))
        {
            message = "快捷键无效，请输入类似 F7、Ctrl+R、Ctrl+Shift+P 这样的格式。";
            return false;
        }

        binding = parsed;
        Save();
        message = $"{label}已更新为 {binding.DisplayText}。";
        return true;
    }

    private static void ApplyShortcutOrDefault(
        string shortcutText,
        ref ShortcutBinding binding,
        string defaultText,
        Key defaultKey,
        bool ctrl = false,
        bool alt = false,
        bool shift = false)
    {
        if (TryParseShortcut(shortcutText, out ShortcutBinding parsed))
        {
            binding = parsed;
            return;
        }

        binding = CreateDefaultBinding(defaultText, defaultKey, ctrl, alt, shift);
    }

    private static ShortcutBinding CreateDefaultBinding(
        string displayText,
        Key key,
        bool ctrl = false,
        bool alt = false,
        bool shift = false)
    {
        return new ShortcutBinding(key, ctrl, alt, shift, displayText);
    }

    private static bool TryParseShortcut(string? shortcutText, out ShortcutBinding binding)
    {
        binding = default;
        string normalized = (shortcutText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        string[] parts = normalized.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        bool ctrl = false;
        bool alt = false;
        bool shift = false;
        Key? key = null;

        foreach (string rawPart in parts)
        {
            string part = rawPart.Trim();
            if (part.Equals("CTRL", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("CONTROL", StringComparison.OrdinalIgnoreCase))
            {
                ctrl = true;
                continue;
            }

            if (part.Equals("ALT", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
                continue;
            }

            if (part.Equals("SHIFT", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
                continue;
            }

            if (key is not null)
            {
                return false;
            }

            if (!Enum.TryParse(part, true, out Key parsedKey) || IsModifierKey(parsedKey))
            {
                return false;
            }

            key = parsedKey;
        }

        if (key is null)
        {
            return false;
        }

        binding = new ShortcutBinding(key.Value, ctrl, alt, shift, BuildShortcutText(key.Value, ctrl, alt, shift));
        return true;
    }

    private static string BuildShortcutText(Key key, bool ctrl, bool alt, bool shift)
    {
        StringBuilder builder = new();
        if (ctrl)
        {
            builder.Append("Ctrl+");
        }

        if (alt)
        {
            builder.Append("Alt+");
        }

        if (shift)
        {
            builder.Append("Shift+");
        }

        builder.Append(key.ToString().ToUpperInvariant());
        return builder.ToString();
    }

    private static bool MatchesShortcut(InputEventKey keyEvent, ShortcutBinding binding)
    {
        if (!keyEvent.Pressed || keyEvent.Echo)
        {
            return false;
        }

        // 快捷键匹配走“键值 + 修饰键完全一致”，避免 Ctrl+R 和单独 R 混在一起误触。
        return keyEvent.Keycode == binding.Key &&
               keyEvent.CtrlPressed == binding.Ctrl &&
               keyEvent.AltPressed == binding.Alt &&
               keyEvent.ShiftPressed == binding.Shift;
    }

    private static bool IsModifierKey(Key key)
    {
        string text = key.ToString();
        return text.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("Shift", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("Alt", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("Meta", StringComparison.OrdinalIgnoreCase);
    }

    private static void Save()
    {
        try
        {
            SettingsData data = new()
            {
                ToggleKey = _toggleShortcut.DisplayText,
                ResetShortcut = _resetShortcut.DisplayText,
                PackBrowserShortcut = _packBrowserShortcut.DisplayText,
                PickerShortcut = _pickerShortcut.DisplayText,
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
            GD.PushWarning($"[CardArtEditor][UIV2] Failed to save shortcut settings: {ex.Message}");
        }
    }

    private static string GetConfigPath()
    {
        string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return Path.Combine(assemblyDir ?? ".", "card_art_editor_settings.json");
    }
}
