using Godot;
using InfectionCardReplacer.UIV2.Services;
using System;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class CleanSettingsDialogView : Window
{
    private enum ShortcutSlot
    {
        None,
        ToggleUi,
        ResetCard,
        PackBrowser,
        PickerDrawer
    }

    private Label? _toggleShortcutValue;
    private Label? _resetShortcutValue;
    private Label? _packBrowserShortcutValue;
    private Label? _pickerShortcutValue;
    private Label? _statusLabel;
    private ShortcutSlot _capturingSlot;

    public override void _Ready()
    {
        Title = "设置";
        InitialPosition = WindowInitialPosition.CenterMainWindowScreen;
        Size = new Vector2I(760, 420);
        Visible = false;
        CloseRequested += Hide;
        VisibilityChanged += RefreshShortcutTexts;

        VBoxContainer layout = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 20.0f,
            OffsetTop = 20.0f,
            OffsetRight = -20.0f,
            OffsetBottom = -20.0f
        };
        layout.AddThemeConstantOverride("separation", 12);
        AddChild(layout);

        layout.AddChild(new Label { Text = "快捷键设置" });
        layout.AddChild(new Label { Text = "点击“修改”后直接按下新的快捷键组合，按 Esc 可以取消本次捕获。" });

        layout.AddChild(BuildShortcutRow("打开/关闭主界面", out _toggleShortcutValue, () => BeginCapture(ShortcutSlot.ToggleUi, "打开/关闭主界面")));
        layout.AddChild(BuildShortcutRow("恢复当前卡默认", out _resetShortcutValue, () => BeginCapture(ShortcutSlot.ResetCard, "恢复当前卡默认")));
        layout.AddChild(BuildShortcutRow("打开/关闭浏览卡包", out _packBrowserShortcutValue, () => BeginCapture(ShortcutSlot.PackBrowser, "打开/关闭浏览卡包")));
        layout.AddChild(BuildShortcutRow("打开/关闭选图栏", out _pickerShortcutValue, () => BeginCapture(ShortcutSlot.PickerDrawer, "打开/关闭选图栏")));

        HBoxContainer actionRow = new();
        actionRow.AddThemeConstantOverride("separation", 8);
        layout.AddChild(actionRow);

        Button resetDefaultsButton = new()
        {
            Text = "恢复默认快捷键",
            CustomMinimumSize = new Vector2(160.0f, 36.0f)
        };
        resetDefaultsButton.Pressed += ResetDefaults;
        actionRow.AddChild(resetDefaultsButton);

        Button closeButton = new()
        {
            Text = "关闭",
            CustomMinimumSize = new Vector2(120.0f, 36.0f)
        };
        closeButton.Pressed += Hide;
        actionRow.AddChild(closeButton);

        _statusLabel = new Label
        {
            Text = "状态：等待修改",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        layout.AddChild(_statusLabel);

        RefreshShortcutTexts();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || _capturingSlot == ShortcutSlot.None || @event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        GetViewport()?.SetInputAsHandled();

        if (keyEvent.Keycode == Key.Escape && !keyEvent.CtrlPressed && !keyEvent.AltPressed && !keyEvent.ShiftPressed)
        {
            _capturingSlot = ShortcutSlot.None;
            SetStatus("状态：已取消本次快捷键修改。", false);
            return;
        }

        if (!ShortcutSettingsService.TryFormatShortcut(keyEvent, out string shortcutText))
        {
            SetStatus("状态：请按下一个有效的非修饰键组合。", true);
            return;
        }

        bool isSuccess = ApplyCapturedShortcut(shortcutText, out string message);
        _capturingSlot = ShortcutSlot.None;
        RefreshShortcutTexts();
        SetStatus($"状态：{message}", !isSuccess);
    }

    private static Control BuildShortcutRow(string title, out Label valueLabel, Action onEdit)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 10);

        Label titleLabel = new()
        {
            Text = title,
            CustomMinimumSize = new Vector2(220.0f, 0.0f)
        };
        row.AddChild(titleLabel);

        valueLabel = new Label
        {
            Text = "-",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.AddChild(valueLabel);

        Button editButton = new()
        {
            Text = "修改",
            CustomMinimumSize = new Vector2(96.0f, 34.0f)
        };
        editButton.Pressed += onEdit;
        row.AddChild(editButton);

        return row;
    }

    private void BeginCapture(ShortcutSlot slot, string label)
    {
        _capturingSlot = slot;
        SetStatus($"状态：请按下“{label}”的新快捷键，Esc 取消。", false);
    }

    private bool ApplyCapturedShortcut(string shortcutText, out string message)
    {
        return _capturingSlot switch
        {
            ShortcutSlot.ToggleUi => ShortcutSettingsService.TryUpdateToggleShortcut(shortcutText, out message),
            ShortcutSlot.ResetCard => ShortcutSettingsService.TryUpdateResetShortcut(shortcutText, out message),
            ShortcutSlot.PackBrowser => ShortcutSettingsService.TryUpdatePackBrowserShortcut(shortcutText, out message),
            ShortcutSlot.PickerDrawer => ShortcutSettingsService.TryUpdatePickerShortcut(shortcutText, out message),
            _ => FailNoTarget(out message)
        };
    }

    private void RefreshShortcutTexts()
    {
        if (_toggleShortcutValue is null || _resetShortcutValue is null || _packBrowserShortcutValue is null || _pickerShortcutValue is null)
        {
            return;
        }

        ShortcutSettingsService.EnsureLoaded();
        _toggleShortcutValue.Text = ShortcutSettingsService.ToggleShortcutText;
        _resetShortcutValue.Text = ShortcutSettingsService.ResetShortcutText;
        _packBrowserShortcutValue.Text = ShortcutSettingsService.PackBrowserShortcutText;
        _pickerShortcutValue.Text = ShortcutSettingsService.PickerShortcutText;
    }

    private void ResetDefaults()
    {
        ShortcutSettingsService.ResetShortcutsToDefault();
        _capturingSlot = ShortcutSlot.None;
        RefreshShortcutTexts();
        SetStatus("状态：快捷键已恢复为默认值。", false);
    }

    private void SetStatus(string message, bool isError)
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Text = message;
        _statusLabel.Modulate = isError
            ? new Color(1.0f, 0.72f, 0.72f, 1.0f)
            : new Color(0.84f, 0.96f, 0.86f, 1.0f);
    }

    private static bool FailNoTarget(out string message)
    {
        message = "当前没有正在修改的快捷键。";
        return false;
    }
}
