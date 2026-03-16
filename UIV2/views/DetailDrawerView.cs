using Godot;
using System;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class DetailDrawerView : PanelContainer
{
    public event Action? BrowseImageClicked;
    public event Action? ApplyClicked;
    public event Action? ResetClicked;

    private Label? _cardNameLabel;
    private Label? _cardIdLabel;
    private Label? _cardTypeLabel;
    private Label? _statusLabel;
    private LineEdit? _imagePathInput;

    public override void _Ready()
    {
        Modulate = Colors.White;
        Visible = false;

        AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.10f, 0.09f, 0.07f, 0.44f),
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

        layout.AddChild(new Label { Text = "详情抽屉" });

        _cardNameLabel = new Label { Text = "当前卡牌：未选择" };
        layout.AddChild(_cardNameLabel);

        _cardIdLabel = new Label
        {
            Text = "cardId：-",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        layout.AddChild(_cardIdLabel);

        _cardTypeLabel = new Label { Text = "卡牌类型：-" };
        layout.AddChild(_cardTypeLabel);

        layout.AddChild(new Label { Text = "图片路径" });

        _imagePathInput = new LineEdit
        {
            PlaceholderText = "选择一张本地图片",
            ClearButtonEnabled = true
        };
        layout.AddChild(_imagePathInput);

        Button browseButton = new()
        {
            Text = "选择图片",
            CustomMinimumSize = new Vector2(120.0f, 36.0f)
        };
        browseButton.Pressed += () => BrowseImageClicked?.Invoke();
        layout.AddChild(browseButton);

        HBoxContainer actionRow = new();
        actionRow.AddThemeConstantOverride("separation", 8);
        layout.AddChild(actionRow);

        Button applyButton = new()
        {
            Text = "应用替换",
            CustomMinimumSize = new Vector2(120.0f, 36.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        applyButton.Pressed += () => ApplyClicked?.Invoke();
        actionRow.AddChild(applyButton);

        Button resetButton = new()
        {
            Text = "恢复默认",
            CustomMinimumSize = new Vector2(120.0f, 36.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        resetButton.Pressed += () => ResetClicked?.Invoke();
        actionRow.AddChild(resetButton);

        _statusLabel = new Label
        {
            Text = "状态：等待操作",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        layout.AddChild(_statusLabel);
    }

    public void ShowCard(SkinCenterCardItem item, SkinCenterPreviewData preview)
    {
        Visible = true;

        if (_cardNameLabel is not null)
        {
            _cardNameLabel.Text = $"当前卡牌：{item.CardName}";
        }

        if (_cardIdLabel is not null)
        {
            _cardIdLabel.Text = $"cardId：{item.CardId}";
        }

        if (_cardTypeLabel is not null)
        {
            _cardTypeLabel.Text = string.IsNullOrWhiteSpace(preview.CardType)
                ? "卡牌类型：-"
                : $"卡牌类型：{preview.CardType}";
        }

        if (_imagePathInput is not null)
        {
            _imagePathInput.Text = preview.ReplacementPath;
        }
    }

    public void Collapse()
    {
        Visible = false;
    }

    public string GetImagePath()
    {
        return _imagePathInput?.Text?.Trim() ?? string.Empty;
    }

    public void SetImagePath(string? imagePath)
    {
        if (_imagePathInput is not null)
        {
            _imagePathInput.Text = imagePath ?? string.Empty;
        }
    }

    public void SetStatus(string status)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"状态：{status}";
        }
    }
}
