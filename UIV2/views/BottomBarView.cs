using Godot;
using System;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class BottomBarView : PanelContainer
{
    public event Action? ApplyClicked;
    public event Action? ResetClicked;
    public event Action? ResetAllClicked;

    private Label? _statusLabel;

    public override void _Ready()
    {
        Modulate = new Color(0.92f, 0.96f, 1.0f, 0.72f);

        AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.05f, 0.10f, 0.14f, 0.42f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        HBoxContainer layout = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 14.0f,
            OffsetTop = 8.0f,
            OffsetRight = -14.0f,
            OffsetBottom = -8.0f
        };
        layout.AddThemeConstantOverride("separation", 8);
        AddChild(layout);

        _statusLabel = new Label
        {
            Text = "等待操作",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        layout.AddChild(_statusLabel);

        Button applyButton = new()
        {
            Text = "应用替换",
            CustomMinimumSize = new Vector2(120.0f, 36.0f)
        };
        applyButton.Pressed += () => ApplyClicked?.Invoke();
        layout.AddChild(applyButton);

        Button resetButton = new()
        {
            Text = "恢复默认",
            CustomMinimumSize = new Vector2(120.0f, 36.0f)
        };
        resetButton.Pressed += () => ResetClicked?.Invoke();
        layout.AddChild(resetButton);

        Button resetAllButton = new()
        {
            Text = "恢复全部默认",
            CustomMinimumSize = new Vector2(148.0f, 36.0f)
        };
        resetAllButton.Pressed += () => ResetAllClicked?.Invoke();
        layout.AddChild(resetAllButton);
    }

    public void SetStatus(string text)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = text;
        }
    }
}
