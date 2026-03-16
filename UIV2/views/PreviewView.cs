using Godot;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class PreviewView : PanelContainer
{
    private const float CompareCardBaseWidth = 248.0f;
    private const float CompareCardBaseHeight = 356.0f;
    private const float CompareCardSeparation = 28.0f;

    private sealed class CardPreviewWidgets
    {
        public Control Root { get; init; } = null!;
        public ColorRect CardBackground { get; init; } = null!;
        public ColorRect BodyBackground { get; init; } = null!;
        public TextureRect Frame { get; init; } = null!;
        public TextureRect Highlight { get; init; } = null!;
        public TextureRect Banner { get; init; } = null!;
        public TextureRect Portrait { get; init; } = null!;
        public ColorRect PortraitBackground { get; init; } = null!;
        public TextureRect PortraitBorder { get; init; } = null!;
        public TextureRect EnergyIcon { get; init; } = null!;
        public Label EnergyCost { get; init; } = null!;
        public Label Title { get; init; } = null!;
        public Label Type { get; init; } = null!;
        public Label Description { get; init; } = null!;
        public Label EmptyHint { get; init; } = null!;
    }

    private enum PreviewDisplayMode
    {
        Original,
        Replacement,
        Compare
    }

    private Label? _cardNameLabel;
    private Label? _cardTypeLabel;
    private Label? _cardIdLabel;
    private Button? _originalButton;
    private Button? _replacementButton;
    private Button? _compareButton;
    private Control? _singlePreviewContainer;
    private Control? _comparePreviewContainer;
    private Control? _leftCompareSlot;
    private Control? _rightCompareSlot;
    private CardPreviewWidgets? _singleCardPreview;
    private CardPreviewWidgets? _leftCardPreview;
    private CardPreviewWidgets? _rightCardPreview;
    private SkinCenterPreviewData _currentData = new();
    private PreviewDisplayMode _displayMode = PreviewDisplayMode.Original;

    public override void _Ready()
    {
        Modulate = Colors.White;

        AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.05f, 0.06f, 0.08f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        VBoxContainer layout = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 20.0f,
            OffsetTop = 18.0f,
            OffsetRight = -20.0f,
            OffsetBottom = -18.0f
        };
        layout.AddThemeConstantOverride("separation", 12);
        AddChild(layout);

        layout.AddChild(new Label { Text = "预览区" });

        HBoxContainer modeRow = new();
        modeRow.AddThemeConstantOverride("separation", 8);
        layout.AddChild(modeRow);
        _originalButton = CreateModeButton("原卡图", PreviewDisplayMode.Original);
        _replacementButton = CreateModeButton("选择图", PreviewDisplayMode.Replacement);
        _compareButton = CreateModeButton("并排对比", PreviewDisplayMode.Compare);
        modeRow.AddChild(_originalButton);
        modeRow.AddChild(_replacementButton);
        modeRow.AddChild(_compareButton);

        _cardNameLabel = new Label { Text = "当前卡牌：未检测到" };
        layout.AddChild(_cardNameLabel);

        _cardTypeLabel = new Label { Text = "卡牌类型：未检测到" };
        layout.AddChild(_cardTypeLabel);

        _cardIdLabel = new Label { Text = "cardId：-" };
        _cardIdLabel.AddThemeFontSizeOverride("font_size", 14);
        layout.AddChild(_cardIdLabel);

        Control previewCenter = new Control
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0.0f, 520.0f)
        };
        layout.AddChild(previewCenter);

        _singlePreviewContainer = new CenterContainer
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        previewCenter.AddChild(_singlePreviewContainer);

        _singleCardPreview = CreateCardPreviewWidgets(new Vector2(356.0f, 512.0f));
        _singlePreviewContainer.AddChild(_singleCardPreview.Root);

        _comparePreviewContainer = new HBoxContainer
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _comparePreviewContainer.AddThemeConstantOverride("separation", (int)CompareCardSeparation);
        _comparePreviewContainer.Resized += UpdateCompareLayout;
        previewCenter.AddChild(_comparePreviewContainer);

        _leftCardPreview = CreateCardPreviewWidgets(new Vector2(CompareCardBaseWidth, CompareCardBaseHeight), compact: true);
        _rightCardPreview = CreateCardPreviewWidgets(new Vector2(CompareCardBaseWidth, CompareCardBaseHeight), compact: true);
        _leftCardPreview.Root.PivotOffset = new Vector2(CompareCardBaseWidth * 0.5f, CompareCardBaseHeight * 0.5f);
        _rightCardPreview.Root.PivotOffset = new Vector2(CompareCardBaseWidth * 0.5f, CompareCardBaseHeight * 0.5f);
        _leftCompareSlot = CreateCompareCardSlot(_leftCardPreview.Root);
        _rightCompareSlot = CreateCompareCardSlot(_rightCardPreview.Root);
        _comparePreviewContainer.AddChild(_leftCompareSlot);
        _comparePreviewContainer.AddChild(_rightCompareSlot);

        RefreshModeButtons();
        RefreshPreviewTextures();
        UpdateCompareLayout();
    }

    public void ShowPreview(SkinCenterPreviewData data)
    {
        _currentData = data;

        if (_cardNameLabel is not null)
        {
            _cardNameLabel.Text = string.IsNullOrWhiteSpace(data.CardName)
                ? "当前卡牌：未检测到"
                : $"当前卡牌：{data.CardName}";
        }

        if (_cardTypeLabel is not null)
        {
            _cardTypeLabel.Text = string.IsNullOrWhiteSpace(data.CardType)
                ? "卡牌类型：未检测到"
                : $"卡牌类型：{data.CardType}";
        }

        if (_cardIdLabel is not null)
        {
            _cardIdLabel.Text = string.IsNullOrWhiteSpace(data.CardId)
                ? "cardId：-"
                : $"cardId：{data.CardId}";
        }

        if ((_displayMode == PreviewDisplayMode.Replacement || _displayMode == PreviewDisplayMode.Compare) && data.ReplacementTexture is null)
        {
            _displayMode = PreviewDisplayMode.Original;
        }

        RefreshModeButtons();
        RefreshPreviewTextures();
    }

    private Button CreateModeButton(string text, PreviewDisplayMode mode)
    {
        Button button = new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(120.0f, 36.0f)
        };
        button.Pressed += () =>
        {
            _displayMode = mode;
            RefreshModeButtons();
            RefreshPreviewTextures();
        };
        return button;
    }

    private void RefreshModeButtons()
    {
        bool hasReplacement = _currentData.ReplacementTexture is not null;
        SetButtonState(_originalButton, _displayMode == PreviewDisplayMode.Original);
        SetButtonState(_replacementButton, _displayMode == PreviewDisplayMode.Replacement);
        SetButtonState(_compareButton, _displayMode == PreviewDisplayMode.Compare);

        if (_replacementButton is not null)
        {
            _replacementButton.Disabled = !hasReplacement;
        }

        if (_compareButton is not null)
        {
            _compareButton.Disabled = !hasReplacement;
        }
    }

    private void RefreshPreviewTextures()
    {
        if (_singlePreviewContainer is null || _comparePreviewContainer is null)
        {
            return;
        }

        bool compareMode = _displayMode == PreviewDisplayMode.Compare;
        _singlePreviewContainer.Visible = !compareMode;
        _comparePreviewContainer.Visible = compareMode;

        if (_singleCardPreview is not null)
        {
            ApplyCardPreview(_singleCardPreview, _currentData, _displayMode == PreviewDisplayMode.Replacement);
        }

        if (_leftCardPreview is not null)
        {
            ApplyCardPreview(_leftCardPreview, _currentData, useReplacement: false);
        }

        if (_rightCardPreview is not null)
        {
            ApplyCardPreview(_rightCardPreview, _currentData, useReplacement: true);
        }

        UpdateCompareLayout();
    }

    private static void ApplyCardPreview(CardPreviewWidgets widgets, SkinCenterPreviewData data, bool useReplacement)
    {
        bool hasCard = data.FrameTexture is not null || data.BannerTexture is not null || data.OriginalTexture is not null;
        Texture2D? portraitTexture = useReplacement ? data.ReplacementTexture : data.OriginalTexture;

        widgets.Frame.Texture = data.FrameTexture;
        widgets.Frame.Material = data.FrameMaterial;
        widgets.Frame.Modulate = data.FrameModulate;
        widgets.Highlight.Texture = data.HighlightTexture;
        widgets.Highlight.Material = data.HighlightMaterial;
        widgets.Highlight.Modulate = data.HighlightModulate;
        widgets.Highlight.Visible = data.HighlightTexture is not null;
        widgets.Banner.Texture = data.BannerTexture;
        widgets.Banner.Material = data.BannerMaterial;
        widgets.Banner.Modulate = data.BannerModulate;
        widgets.Portrait.Texture = portraitTexture;
        widgets.Portrait.StretchMode = useReplacement
            ? TextureRect.StretchModeEnum.KeepAspectCentered
            : TextureRect.StretchModeEnum.KeepAspectCovered;
        widgets.PortraitBorder.Texture = data.PortraitBorderTexture;
        widgets.PortraitBorder.Material = data.PortraitBorderMaterial;
        widgets.PortraitBorder.Modulate = data.PortraitBorderModulate;
        widgets.EnergyIcon.Texture = data.EnergyIconTexture;
        widgets.EnergyIcon.Modulate = data.EnergyIconModulate;
        widgets.EnergyCost.Text = data.EnergyText;
        widgets.Title.Text = string.IsNullOrWhiteSpace(data.CardName)
            ? (useReplacement ? "选择图" : "原卡图")
            : data.CardName;
        widgets.Type.Text = data.CardType;
        widgets.Description.Text = data.Description;
        widgets.CardBackground.Visible = hasCard;
        widgets.BodyBackground.Visible = hasCard;
        widgets.PortraitBackground.Visible = portraitTexture is null;
        widgets.EmptyHint.Visible = portraitTexture is null;
        widgets.EmptyHint.Text = hasCard
            ? (useReplacement ? "当前还没有选择图片" : "当前卡牌没有可用原图")
            : "当前还没有选择卡牌";
    }

    private static CardPreviewWidgets CreateCardPreviewWidgets(Vector2 size, bool compact = false)
    {
        float width = size.X;
        float height = size.Y;
        float portraitMarginX = compact ? 26.0f : 30.0f;
        float portraitTop = compact ? 40.0f : 50.0f;
        float portraitHeight = compact ? height * 0.31f : height * 0.34f;
        float bannerHeight = compact ? 30.0f : 38.0f;
        float energySize = compact ? 38.0f : 48.0f;
        float typeHeight = compact ? 22.0f : 26.0f;
        float titleFontSize = compact ? 16.0f : 22.0f;
        float typeFontSize = compact ? 11.0f : 14.0f;
        float descFontSize = compact ? 10.0f : 13.0f;
        float energyFontSize = compact ? 20.0f : 28.0f;
        float textTop = portraitTop + portraitHeight + (compact ? 42.0f : 52.0f);
        float bodyTop = portraitTop + portraitHeight + (compact ? 20.0f : 26.0f);
        float bodyHeight = height - bodyTop - (compact ? 12.0f : 18.0f);

        Control root = new()
        {
            CustomMinimumSize = size,
            Size = size,
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        ColorRect cardBackground = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.14f, 0.11f, 0.08f, 0.94f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(cardBackground);

        ColorRect bodyBackground = new()
        {
            Position = new Vector2(22.0f, bodyTop),
            Size = new Vector2(width - 44.0f, bodyHeight),
            Color = compact
                ? new Color(0.83f, 0.77f, 0.67f, 0.74f)
                : new Color(0.85f, 0.78f, 0.69f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(bodyBackground);

        TextureRect frame = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(frame);

        TextureRect highlight = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        root.AddChild(highlight);

        ColorRect portraitBackground = new()
        {
            Position = new Vector2(portraitMarginX, portraitTop),
            Size = new Vector2(width - portraitMarginX * 2.0f, portraitHeight),
            Color = new Color(0.08f, 0.08f, 0.08f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(portraitBackground);

        TextureRect portrait = new()
        {
            Position = portraitBackground.Position,
            Size = portraitBackground.Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(portrait);

        TextureRect portraitBorder = new()
        {
            Position = portrait.Position,
            Size = portrait.Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(portraitBorder);

        TextureRect banner = new()
        {
            Position = new Vector2(12.0f, 8.0f),
            Size = new Vector2(width - 24.0f, bannerHeight),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(banner);

        TextureRect energyIcon = new()
        {
            Position = new Vector2(6.0f, 2.0f),
            Size = new Vector2(energySize, energySize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(energyIcon);

        Label energyCost = new()
        {
            Position = new Vector2(6.0f, 4.0f),
            Size = new Vector2(energySize, energySize),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        energyCost.AddThemeFontSizeOverride("font_size", (int)energyFontSize);
        root.AddChild(energyCost);

        Label title = new()
        {
            Position = new Vector2(56.0f, compact ? 7.0f : 9.0f),
            Size = new Vector2(width - 112.0f, bannerHeight),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", (int)titleFontSize);
        root.AddChild(title);

        Label type = new()
        {
            Position = new Vector2(width * 0.5f - 42.0f, portrait.Position.Y + portrait.Size.Y - 4.0f),
            Size = new Vector2(84.0f, typeHeight),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        type.AddThemeFontSizeOverride("font_size", (int)typeFontSize);
        root.AddChild(type);

        Label description = new()
        {
            Position = new Vector2(28.0f, textTop),
            Size = new Vector2(width - 56.0f, height - textTop - 22.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = compact ? 5 : 7
        };
        description.AddThemeFontSizeOverride("font_size", (int)descFontSize);
        root.AddChild(description);

        Label emptyHint = new()
        {
            Position = new Vector2(portraitMarginX + 8.0f, portraitTop + portraitHeight * 0.5f - 16.0f),
            Size = new Vector2(width - portraitMarginX * 2.0f - 16.0f, 32.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false
        };
        emptyHint.AddThemeFontSizeOverride("font_size", compact ? 12 : 14);
        root.AddChild(emptyHint);

        return new CardPreviewWidgets
        {
            Root = root,
            CardBackground = cardBackground,
            BodyBackground = bodyBackground,
            Frame = frame,
            Highlight = highlight,
            Banner = banner,
            Portrait = portrait,
            PortraitBackground = portraitBackground,
            PortraitBorder = portraitBorder,
            EnergyIcon = energyIcon,
            EnergyCost = energyCost,
            Title = title,
            Type = type,
            Description = description,
            EmptyHint = emptyHint
        };
    }

    private static void SetButtonState(Button? button, bool active)
    {
        if (button is null)
        {
            return;
        }

        button.Modulate = active
            ? new Color(1.0f, 0.98f, 0.88f, 1.0f)
            : Colors.White;
    }

    private void UpdateCompareLayout()
    {
        if (_comparePreviewContainer is not HBoxContainer compareContainer ||
            _leftCardPreview is null ||
            _rightCardPreview is null)
        {
            return;
        }

        float availableWidth = compareContainer.Size.X;
        float availableHeight = compareContainer.Size.Y;
        if (availableWidth <= 0.0f || availableHeight <= 0.0f)
        {
            return;
        }

        float slotWidth = Math.Max(1.0f, (availableWidth - CompareCardSeparation) * 0.5f);
        float widthScale = slotWidth / CompareCardBaseWidth;
        float heightScale = availableHeight / CompareCardBaseHeight;
        float scale = MathF.Min(1.0f, MathF.Min(widthScale, heightScale));

        // 并排模式按可用宽高统一缩放两张卡，避免横向裁切或把卡面撑爆。
        Vector2 scaledSize = new(CompareCardBaseWidth * scale, CompareCardBaseHeight * scale);
        _leftCardPreview.Root.Scale = new Vector2(scale, scale);
        _rightCardPreview.Root.Scale = new Vector2(scale, scale);
        if (_leftCompareSlot is not null)
        {
            _leftCompareSlot.CustomMinimumSize = scaledSize;
        }

        if (_rightCompareSlot is not null)
        {
            _rightCompareSlot.CustomMinimumSize = scaledSize;
        }
    }

    private static Control CreateCompareCardSlot(Control cardRoot)
    {
        CenterContainer slot = new()
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        slot.AddChild(cardRoot);
        return slot;
    }
}
