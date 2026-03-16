using Godot;
using System.Collections.Generic;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class HistoryDialogView : Window
{
    private VBoxContainer? _listContainer;

    public override void _Ready()
    {
        Title = "历史记录";
        InitialPosition = WindowInitialPosition.CenterMainWindowScreen;
        Size = new Vector2I(760, 520);
        Visible = false;
        CloseRequested += Hide;

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

        layout.AddChild(new Label { Text = "最近的卡面操作记录" });

        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        layout.AddChild(scroll);

        _listContainer = new VBoxContainer();
        _listContainer.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_listContainer);

        Button closeButton = new()
        {
            Text = "关闭",
            CustomMinimumSize = new Vector2(120.0f, 36.0f)
        };
        closeButton.Pressed += Hide;
        layout.AddChild(closeButton);
    }

    public void SetEntries(IReadOnlyList<CardArtHistoryRegistry.HistoryEntry> entries)
    {
        if (_listContainer is null)
        {
            return;
        }

        foreach (Node child in _listContainer.GetChildren())
        {
            child.QueueFree();
        }

        if (entries.Count == 0)
        {
            _listContainer.AddChild(new Label { Text = "暂无历史记录" });
            return;
        }

        foreach (CardArtHistoryRegistry.HistoryEntry entry in entries)
        {
            PanelContainer panel = new();
            VBoxContainer itemLayout = new()
            {
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                OffsetLeft = 12.0f,
                OffsetTop = 10.0f,
                OffsetRight = -12.0f,
                OffsetBottom = -10.0f
            };
            itemLayout.AddThemeConstantOverride("separation", 4);
            panel.AddChild(itemLayout);

            itemLayout.AddChild(new Label { Text = $"{entry.Timestamp} | {entry.Action}" });
            itemLayout.AddChild(new Label { Text = $"目标卡牌：{entry.Target}" });
            itemLayout.AddChild(new Label
            {
                Text = string.IsNullOrWhiteSpace(entry.ImagePath) ? "图片路径：-" : $"图片路径：{entry.ImagePath}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });

            _listContainer.AddChild(panel);
        }
    }
}
