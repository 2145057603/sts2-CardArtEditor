using Godot;
using System;
using System.Collections.Generic;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class ResourceListView : PanelContainer
{
    public event Action<int>? CardSelected;

    private Label? _countLabel;
    private ItemList? _itemList;

    public override void _Ready()
    {
        Modulate = new Color(1.0f, 0.97f, 0.89f, 0.68f);

        AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.10f, 0.07f, 0.06f, 0.42f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        VBoxContainer layout = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 18.0f,
            OffsetTop = 18.0f,
            OffsetRight = -18.0f,
            OffsetBottom = -18.0f
        };
        layout.AddThemeConstantOverride("separation", 12);
        AddChild(layout);

        layout.AddChild(new Label { Text = "资源区" });

        _countLabel = new Label { Text = "已收集 / 已选：0 / 0" };
        layout.AddChild(_countLabel);

        _itemList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        _itemList.ItemSelected += index => CardSelected?.Invoke((int)index);
        layout.AddChild(_itemList);
    }

    public void SetSummary(string text)
    {
        if (_countLabel is not null)
        {
            _countLabel.Text = text;
        }
    }

    public void SetItems(IReadOnlyList<SkinCenterCardItem> items)
    {
        if (_itemList is null)
        {
            return;
        }

        _itemList.Clear();
        foreach (SkinCenterCardItem item in items)
        {
            string suffix = item.HasReplacement ? " [已替换]" : string.Empty;
            _itemList.AddItem($"{item.CardName}{suffix}");
        }
    }
}
