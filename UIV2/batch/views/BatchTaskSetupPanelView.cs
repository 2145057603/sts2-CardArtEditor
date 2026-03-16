using Godot;
using InfectionCardReplacer.PackSystem.Models;
using InfectionCardReplacer.UIV2.Batch.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace InfectionCardReplacer.UIV2.Batch.Views;

internal sealed partial class BatchTaskSetupPanelView : PanelContainer
{
    private const int SourcePreviewColumnWidth = 120;
    private const int SourcePreviewIconSize = 84;
    private const int SourcePreviewMaxCount = 60;

    public event Action<BatchTaskGenerateOptions>? GenerateRequested;
    public event Action? RefreshSourcesRequested;
    public event Action? SourceSelectionChanged;
    public event Action<PackEntry>? SourcePreviewEntrySelected;

    private readonly List<BatchTaskSourceOption> _sourceOptions = [];
    private readonly List<PackEntry> _sourcePreviewEntries = [];
    private readonly Dictionary<string, Texture2D?> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);

    private LineEdit? _taskNameInput;
    private OptionButton? _scopeSelect;
    private OptionButton? _sourceModeSelect;
    private OptionButton? _sourcePackSelect;
    private OptionButton? _matchModeSelect;
    private Label? _sourcePackLabel;
    private Label? _sourcePreviewSummaryLabel;
    private ItemList? _sourcePreviewList;

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.08f, 0.10f, 0.13f, 0.92f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        VBoxContainer layout = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 12.0f,
            OffsetTop = 12.0f,
            OffsetRight = -12.0f,
            OffsetBottom = -12.0f
        };
        layout.AddThemeConstantOverride("separation", 8);
        AddChild(layout);

        layout.AddChild(new Label { Text = "批量任务设置" });
        layout.AddChild(new Label { Text = "任务名称" });

        _taskNameInput = new LineEdit
        {
            PlaceholderText = "例如：Silent_PackBatch",
            Text = $"BatchTask_{DateTime.Now:MMdd_HHmm}"
        };
        layout.AddChild(_taskNameInput);

        layout.AddChild(new Label { Text = "处理范围" });
        _scopeSelect = new OptionButton
        {
            FitToLongestItem = false
        };
        _scopeSelect.AddItem("当前筛选结果");
        _scopeSelect.AddItem("当前角色全部卡牌");
        _scopeSelect.AddItem("全部卡牌");
        _scopeSelect.Select((int)BatchTaskScope.AllCards);
        layout.AddChild(_scopeSelect);

        layout.AddChild(new Label { Text = "来源模式" });
        _sourceModeSelect = new OptionButton
        {
            FitToLongestItem = false
        };
        _sourceModeSelect.AddItem("全部启用卡包");
        _sourceModeSelect.AddItem("单一卡包");
        _sourceModeSelect.AddItem("仅发布包");
        _sourceModeSelect.Select((int)BatchTaskSourceMode.SinglePack);
        _sourceModeSelect.ItemSelected += _ =>
        {
            RefreshSourcePackVisibility();
            SourceSelectionChanged?.Invoke();
        };
        layout.AddChild(_sourceModeSelect);

        _sourcePackLabel = new Label
        {
            Text = "来源卡包"
        };
        layout.AddChild(_sourcePackLabel);

        _sourcePackSelect = new OptionButton
        {
            FitToLongestItem = false
        };
        _sourcePackSelect.ItemSelected += _ => SourceSelectionChanged?.Invoke();
        layout.AddChild(_sourcePackSelect);

        layout.AddChild(new Label { Text = "匹配策略" });
        _matchModeSelect = new OptionButton
        {
            FitToLongestItem = false
        };
        _matchModeSelect.AddItem("显式映射 + 文件名");
        _matchModeSelect.AddItem("仅显式映射");
        _matchModeSelect.AddItem("仅文件名");
        layout.AddChild(_matchModeSelect);

        HBoxContainer actionRow = new();
        actionRow.AddThemeConstantOverride("separation", 8);
        layout.AddChild(actionRow);

        Button refreshButton = new()
        {
            Text = "刷新来源",
            CustomMinimumSize = new Vector2(0.0f, 34.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        refreshButton.Pressed += () => RefreshSourcesRequested?.Invoke();
        actionRow.AddChild(refreshButton);

        Button generateButton = new()
        {
            Text = "生成候选映射",
            CustomMinimumSize = new Vector2(0.0f, 38.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        generateButton.Pressed += () => GenerateRequested?.Invoke(GetCurrentOptions());
        actionRow.AddChild(generateButton);

        layout.AddChild(new HSeparator());
        layout.AddChild(new Label { Text = "来源图片预览" });

        _sourcePreviewSummaryLabel = new Label
        {
            Text = "尚未加载来源图片",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        layout.AddChild(_sourcePreviewSummaryLabel);

        _sourcePreviewList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            IconMode = ItemList.IconModeEnum.Top,
            FixedColumnWidth = SourcePreviewColumnWidth,
            SameColumnWidth = true,
            FixedIconSize = new Vector2I(SourcePreviewIconSize, SourcePreviewIconSize),
            MaxTextLines = 2,
            AllowReselect = true,
            MaxColumns = 3
        };
        _sourcePreviewList.ItemSelected += OnSourcePreviewItemSelected;
        layout.AddChild(_sourcePreviewList);

        RefreshSourcePackVisibility();
    }

    public void SetSourceOptions(IReadOnlyList<BatchTaskSourceOption> options)
    {
        _sourceOptions.Clear();
        _sourceOptions.AddRange(options);

        if (_sourcePackSelect is null)
        {
            return;
        }

        _sourcePackSelect.Clear();
        foreach (BatchTaskSourceOption option in _sourceOptions)
        {
            _sourcePackSelect.AddItem(option.DisplayName);
        }

        if (_sourcePackSelect.ItemCount > 0)
        {
            _sourcePackSelect.Select(0);
        }

        RefreshSourcePackVisibility();
    }

    public BatchTaskGenerateOptions GetCurrentOptions()
    {
        if (_scopeSelect is null || _sourceModeSelect is null || _matchModeSelect is null)
        {
            return new BatchTaskGenerateOptions();
        }

        string packId = string.Empty;
        if (_sourceModeSelect.Selected == (int)BatchTaskSourceMode.SinglePack &&
            _sourcePackSelect is not null &&
            _sourcePackSelect.Selected >= 0 &&
            _sourcePackSelect.Selected < _sourceOptions.Count)
        {
            packId = _sourceOptions[(int)_sourcePackSelect.Selected].PackId;
        }

        return new BatchTaskGenerateOptions
        {
            TaskName = _taskNameInput?.Text?.Trim() ?? string.Empty,
            Scope = (BatchTaskScope)_scopeSelect.Selected,
            SourceMode = (BatchTaskSourceMode)_sourceModeSelect.Selected,
            SelectedPackId = packId,
            MatchMode = (BatchTaskMatchMode)_matchModeSelect.Selected
        };
    }

    public void SetSourcePreviewEntries(IReadOnlyList<PackEntry> entries, string summaryText)
    {
        if (_sourcePreviewSummaryLabel is not null)
        {
            _sourcePreviewSummaryLabel.Text = summaryText;
        }

        if (_sourcePreviewList is null)
        {
            return;
        }

        _sourcePreviewList.Clear();
        _sourcePreviewEntries.Clear();

        int displayCount = Math.Min(SourcePreviewMaxCount, entries.Count);
        for (int index = 0; index < displayCount; index++)
        {
            PackEntry entry = entries[index];
            _sourcePreviewEntries.Add(entry);
            Texture2D? icon = LoadThumbnail(entry.AbsolutePath);
            string displayName = Path.GetFileNameWithoutExtension(entry.FileName);
            _sourcePreviewList.AddItem(displayName, icon);
            _sourcePreviewList.SetItemTooltipEnabled(index, true);
            _sourcePreviewList.SetItemTooltip(index, entry.RelativePath);
        }
    }

    private void OnSourcePreviewItemSelected(long index)
    {
        if (index < 0 || index >= _sourcePreviewEntries.Count)
        {
            return;
        }

        SourcePreviewEntrySelected?.Invoke(_sourcePreviewEntries[(int)index]);
    }

    private void RefreshSourcePackVisibility()
    {
        if (_sourceModeSelect is null || _sourcePackSelect is null || _sourcePackLabel is null)
        {
            return;
        }

        bool showSinglePack = _sourceModeSelect.Selected == (int)BatchTaskSourceMode.SinglePack;
        _sourcePackLabel.Visible = showSinglePack;
        _sourcePackSelect.Visible = showSinglePack;
    }

    private Texture2D? LoadThumbnail(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        if (_thumbnailCache.TryGetValue(path, out Texture2D? cached))
        {
            return cached;
        }

        try
        {
            Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            if (image.Load(path) != Error.Ok)
            {
                _thumbnailCache[path] = null;
                return null;
            }

            // 这里只缓存来源缩略图，避免批量窗口每次打开都重新解码大图。
            image.Resize(SourcePreviewIconSize, SourcePreviewIconSize, Image.Interpolation.Lanczos);
            Texture2D texture = ImageTexture.CreateFromImage(image);
            _thumbnailCache[path] = texture;
            return texture;
        }
        catch
        {
            _thumbnailCache[path] = null;
            return null;
        }
    }
}
