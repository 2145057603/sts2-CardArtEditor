using Godot;
using InfectionCardReplacer.PackSystem.Models;
using InfectionCardReplacer.PackSystem.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class PackImagePickerDrawerView : PanelContainer
{
    public event Func<string, string>? UseImageRequested;
    public event Func<string, string>? ApplyImageRequested;
    public event Func<PackDescriptor, string>? ApplyReleasePackRequested;

    private const float ExpandedWidth = 460.0f;
    private const float CollapsedWidth = 0.0f;
    private const int EntryColumnWidth = 138;

    private readonly PackRegistryService _registryService = new();
    private readonly PackIndexBuilder _indexBuilder = new();
    private readonly UserPackStateService _userStateService = new();
    private readonly PackEntryUserStateService _packEntryUserStateService = new();
    private readonly Dictionary<string, PackIndex> _packIndexCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D?> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D?> _previewTextureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _entryNoteLookup = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<PackDescriptor> _visibleSources = [];
    private readonly List<PickerEntryItem> _visibleEntries = [];

    private OptionButton? _sourceSelect;
    private LineEdit? _searchInput;
    private ItemList? _entryList;
    private Label? _summaryLabel;
    private Label? _statusLabel;
    private TextureRect? _previewTexture;
    private Label? _previewInfoLabel;
    private Button? _applyReleasePackButton;

    private PickerEntryItem? _selectedEntry;
    private bool _isExpanded;

    public override void _Ready()
    {
        Name = "PackImagePickerDrawer";
        AnchorLeft = 1.0f;
        AnchorTop = 0.0f;
        AnchorRight = 1.0f;
        AnchorBottom = 1.0f;
        OffsetLeft = 0.0f;
        OffsetTop = 54.0f;
        OffsetRight = 0.0f;
        OffsetBottom = 0.0f;
        CustomMinimumSize = new Vector2(0.0f, 0.0f);
        SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        Visible = true;
        Modulate = new Color(1.0f, 1.0f, 1.0f, 0.98f);
        MouseFilter = MouseFilterEnum.Stop;

        AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.04f, 0.07f, 0.09f, 0.96f),
            MouseFilter = MouseFilterEnum.Ignore
        });

        VBoxContainer layout = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 14.0f,
            OffsetTop = 14.0f,
            OffsetRight = -14.0f,
            OffsetBottom = -14.0f
        };
        layout.AddThemeConstantOverride("separation", 10);
        AddChild(layout);

        layout.AddChild(BuildHeader());
        layout.AddChild(BuildSourceRow());

        _searchInput = new LineEdit
        {
            PlaceholderText = "搜索文件名、路径、卡包名或图片备注",
            ClearButtonEnabled = true
        };
        _searchInput.TextChanged += _ => RefreshEntries();
        layout.AddChild(_searchInput);

        _summaryLabel = new Label
        {
            Text = "图片数量：0"
        };
        layout.AddChild(_summaryLabel);

        _entryList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            IconMode = ItemList.IconModeEnum.Top,
            FixedColumnWidth = EntryColumnWidth,
            SameColumnWidth = true,
            FixedIconSize = new Vector2I(92, 92),
            MaxTextLines = 2
        };
        _entryList.Resized += UpdateEntryGridLayout;
        _entryList.ItemSelected += ShowSelectedEntry;
        _entryList.ItemActivated += ApplySelectedEntry;
        layout.AddChild(_entryList);

        layout.AddChild(BuildPreviewPanel());

        _statusLabel = new Label
        {
            Text = "状态：右侧选图栏已就绪",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        layout.AddChild(_statusLabel);

        ApplyExpandedState(false);
    }

    public void ToggleDrawer()
    {
        ApplyExpandedState(!_isExpanded);
    }

    public void OpenDrawer()
    {
        ApplyExpandedState(true);
    }

    public void CloseDrawer()
    {
        ApplyExpandedState(false);
    }

    public void RefreshSources(bool force = false)
    {
        if (!force && !_isExpanded)
        {
            return;
        }

        IReadOnlyList<PackDescriptor> packs = _registryService.ScanAvailablePacks(includeLooseFolder: true);
        _visibleSources.Clear();
        _visibleSources.AddRange(packs.Where(pack => IsPackEnabledForSelection(pack.PackId)));

        if (_sourceSelect is null)
        {
            return;
        }

        long previousSelection = _sourceSelect.Selected;
        string? previousPackId = previousSelection > 0 && previousSelection - 1 < _visibleSources.Count
            ? _visibleSources[(int)previousSelection - 1].PackId
            : null;

        _sourceSelect.Clear();
        _sourceSelect.AddItem("全部启用卡包");
        foreach (PackDescriptor descriptor in _visibleSources)
        {
            string releaseSuffix = ReleasePackDescriptorHelper.IsReleasePack(descriptor) ? " [发布包]" : string.Empty;
            _sourceSelect.AddItem($"{descriptor.DisplayName}{releaseSuffix} ({descriptor.SourceType})");
        }

        int selectIndex = 0;
        if (!string.IsNullOrWhiteSpace(previousPackId))
        {
            int matchIndex = _visibleSources.FindIndex(item => item.PackId.Equals(previousPackId, StringComparison.OrdinalIgnoreCase));
            if (matchIndex >= 0)
            {
                selectIndex = matchIndex + 1;
            }
        }

        _sourceSelect.Select(selectIndex);
        RefreshEntries();

        if (_visibleSources.Count == 0)
        {
            SetStatus("状态：当前没有已启用的卡包，请先在卡包管理中启用。", true);
        }
    }

    private Control BuildHeader()
    {
        HBoxContainer header = new();
        header.AddThemeConstantOverride("separation", 8);

        header.AddChild(new Label
        {
            Text = "右侧选图栏",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        });

        Button refreshButton = new()
        {
            Text = "刷新",
            CustomMinimumSize = new Vector2(74.0f, 32.0f)
        };
        refreshButton.Pressed += ForceRefresh;
        header.AddChild(refreshButton);

        Button hideButton = new()
        {
            Text = "隐藏",
            CustomMinimumSize = new Vector2(74.0f, 32.0f)
        };
        hideButton.Pressed += CloseDrawer;
        header.AddChild(hideButton);

        return header;
    }

    private Control BuildSourceRow()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 8);

        _sourceSelect = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _sourceSelect.ItemSelected += _ => RefreshEntries();
        row.AddChild(_sourceSelect);

        return row;
    }

    private Control BuildPreviewPanel()
    {
        VBoxContainer container = new();
        container.AddThemeConstantOverride("separation", 8);

        container.AddChild(new Label { Text = "当前图片预览" });

        PanelContainer previewPanel = new()
        {
            CustomMinimumSize = new Vector2(0.0f, 220.0f)
        };
        container.AddChild(previewPanel);

        CenterContainer previewCenter = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f
        };
        previewPanel.AddChild(previewCenter);

        _previewTexture = new TextureRect
        {
            CustomMinimumSize = new Vector2(240.0f, 180.0f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        previewCenter.AddChild(_previewTexture);

        _previewInfoLabel = new Label
        {
            Text = "请选择图片",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        container.AddChild(_previewInfoLabel);

        HBoxContainer actionRow = new();
        actionRow.AddThemeConstantOverride("separation", 8);
        container.AddChild(actionRow);

        Button useButton = new()
        {
            Text = "设为当前卡",
            CustomMinimumSize = new Vector2(120.0f, 36.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        useButton.Pressed += UseSelectedEntry;
        actionRow.AddChild(useButton);

        Button applyButton = new()
        {
            Text = "直接应用",
            CustomMinimumSize = new Vector2(120.0f, 36.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        applyButton.Pressed += () => ApplySelectedEntry();
        actionRow.AddChild(applyButton);

        _applyReleasePackButton = new Button
        {
            Text = "一键替换",
            CustomMinimumSize = new Vector2(0.0f, 36.0f),
            Visible = false
        };
        // 只有发布包才显示整包应用入口，普通素材包仍然走逐张选图流程。
        _applyReleasePackButton.Pressed += ApplySelectedReleasePack;
        container.AddChild(_applyReleasePackButton);

        return container;
    }

    private void ApplyExpandedState(bool expanded)
    {
        _isExpanded = expanded;
        OffsetLeft = expanded ? -ExpandedWidth : CollapsedWidth;
        MouseFilter = expanded ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;

        if (expanded)
        {
            RefreshSources();
            UpdateEntryGridLayout();
        }
        else
        {
            ClearPreview();
        }
    }

    private void ForceRefresh()
    {
        _packIndexCache.Clear();
        _thumbnailCache.Clear();
        _previewTextureCache.Clear();
        RefreshSources(force: true);
    }

    private void RefreshEntries()
    {
        _visibleEntries.Clear();
        _selectedEntry = null;
        _entryList?.Clear();
        ClearPreview();
        ReloadEntryNoteLookup();

        IEnumerable<PackDescriptor> sources = GetSelectedSources();
        string keyword = _searchInput?.Text?.Trim() ?? string.Empty;

        foreach (PackDescriptor descriptor in sources)
        {
            PackIndex? index = GetOrBuildIndex(descriptor);
            if (index is null)
            {
                continue;
            }

            foreach (PackEntry entry in index.Entries)
            {
                if (!MatchesEntryKeyword(descriptor, entry, keyword))
                {
                    continue;
                }

                _visibleEntries.Add(new PickerEntryItem(descriptor, entry));
            }
        }

        if (_summaryLabel is not null)
        {
            _summaryLabel.Text = $"图片数量：{_visibleEntries.Count}";
        }

        if (_entryList is null)
        {
            return;
        }

        UpdateEntryGridLayout();
        foreach (PickerEntryItem item in _visibleEntries)
        {
            Texture2D? icon = LoadThumbnailTextureCached(item.Entry);
            string subtitle = item.Entry.Width > 0 && item.Entry.Height > 0
                ? $"{item.Entry.Width}x{item.Entry.Height}"
                : item.Descriptor.DisplayName;
            _entryList.AddItem($"{item.Entry.FileName}\n{subtitle}", icon);
        }

        if (_visibleEntries.Count > 0)
        {
            _entryList.Select(0);
            ShowSelectedEntry(0);
        }
    }

    private IEnumerable<PackDescriptor> GetSelectedSources()
    {
        if (_sourceSelect is null || _sourceSelect.Selected <= 0)
        {
            return _visibleSources;
        }

        int index = (int)_sourceSelect.Selected - 1;
        if (index < 0 || index >= _visibleSources.Count)
        {
            return [];
        }

        return [_visibleSources[index]];
    }

    private PackIndex? GetOrBuildIndex(PackDescriptor descriptor)
    {
        string cacheKey = $"{descriptor.PackId}|{descriptor.Version}|{descriptor.RootPath}";
        if (_packIndexCache.TryGetValue(cacheKey, out PackIndex? cached))
        {
            return cached;
        }

        PackIndex? built = _indexBuilder.BuildIndex(descriptor);
        if (built is not null)
        {
            _packIndexCache[cacheKey] = built;
        }

        return built;
    }

    private void ShowSelectedEntry(long index)
    {
        if (index < 0 || index >= _visibleEntries.Count)
        {
            ClearPreview();
            return;
        }

        PickerEntryItem item = _visibleEntries[(int)index];
        _selectedEntry = item;

        Texture2D? texture = LoadPreviewTextureCached(item.Entry);
        string note = GetEntryNote(item.Entry);
        if (_previewTexture is not null)
        {
            _previewTexture.Texture = texture;
        }

        if (_previewInfoLabel is not null)
        {
            string releaseText = ReleasePackDescriptorHelper.IsReleasePack(item.Descriptor) ? "是" : "否";
            _previewInfoLabel.Text =
                $"卡包：{item.Descriptor.DisplayName}\n" +
                $"发布包：{releaseText}\n" +
                $"文件：{item.Entry.FileName}\n" +
                $"路径：{item.Entry.RelativePath}\n" +
                $"尺寸：{item.Entry.Width}x{item.Entry.Height}\n" +
                $"备注：{(string.IsNullOrWhiteSpace(note) ? "无" : note)}";
        }

        if (_applyReleasePackButton is not null)
        {
            _applyReleasePackButton.Visible = ReleasePackDescriptorHelper.IsReleasePack(item.Descriptor);
        }
    }

    private void UseSelectedEntry()
    {
        if (_selectedEntry is null)
        {
            SetStatus("状态：请先选择一张图片。", true);
            return;
        }

        if (UseImageRequested is null)
        {
            SetStatus("状态：当前未接入主界面卡牌选择。", true);
            return;
        }

        if (!IsPackEnabledForSelection(_selectedEntry.Descriptor.PackId))
        {
            SetStatus("状态：当前图片所属卡包未启用。", true);
            return;
        }

        string result = UseImageRequested.Invoke(_selectedEntry.Entry.AbsolutePath);
        SetStatus($"状态：{result}", false);
    }

    private void ApplySelectedEntry(long _ = -1)
    {
        if (_selectedEntry is null)
        {
            SetStatus("状态：请先选择一张图片。", true);
            return;
        }

        if (ApplyImageRequested is null)
        {
            SetStatus("状态：当前未接入主界面卡牌选择。", true);
            return;
        }

        if (!IsPackEnabledForSelection(_selectedEntry.Descriptor.PackId))
        {
            SetStatus("状态：当前图片所属卡包未启用。", true);
            return;
        }

        string result = ApplyImageRequested.Invoke(_selectedEntry.Entry.AbsolutePath);
        SetStatus($"状态：{result}", false);
    }

    private void UpdateEntryGridLayout()
    {
        if (_entryList is null)
        {
            return;
        }

        // 列数随选图栏宽度动态变化，避免窗口放大后仍然只停留在较少列数。
        int columns = Math.Max(1, (int)Math.Floor(_entryList.Size.X / EntryColumnWidth));
        _entryList.MaxColumns = columns;
    }

    private void ClearPreview()
    {
        if (_previewTexture is not null)
        {
            _previewTexture.Texture = null;
        }

        if (_previewInfoLabel is not null)
        {
            _previewInfoLabel.Text = "请选择图片";
        }

        if (_applyReleasePackButton is not null)
        {
            _applyReleasePackButton.Visible = false;
        }
    }

    private void ApplySelectedReleasePack()
    {
        if (_selectedEntry is null)
        {
            SetStatus("状态：请先选择一张图片。", true);
            return;
        }

        if (!ReleasePackDescriptorHelper.IsReleasePack(_selectedEntry.Descriptor))
        {
            SetStatus("状态：当前选中图片不属于发布包。", true);
            return;
        }

        if (!IsPackEnabledForSelection(_selectedEntry.Descriptor.PackId))
        {
            SetStatus("状态：当前发布包未启用。", true);
            return;
        }

        if (ApplyReleasePackRequested is null)
        {
            SetStatus("状态：当前未接入发布包一键替换流程。", true);
            return;
        }

        string result = ApplyReleasePackRequested.Invoke(_selectedEntry.Descriptor);
        bool isError = result.Contains("失败", StringComparison.OrdinalIgnoreCase) ||
                       result.Contains("无效", StringComparison.OrdinalIgnoreCase) ||
                       result.Contains("未", StringComparison.OrdinalIgnoreCase);
        SetStatus($"状态：{result}", isError);
    }

    private void SetStatus(string text, bool isError)
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Text = text;
        _statusLabel.Modulate = isError
            ? new Color(1.0f, 0.72f, 0.72f, 1.0f)
            : new Color(0.84f, 0.96f, 0.86f, 1.0f);
    }

    private bool MatchesEntryKeyword(PackDescriptor descriptor, PackEntry entry, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return entry.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               entry.RelativePath.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               descriptor.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               GetEntryNote(entry).Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private void ReloadEntryNoteLookup()
    {
        _entryNoteLookup.Clear();
        foreach (PackEntryUserState state in _packEntryUserStateService.LoadAll())
        {
            _entryNoteLookup[state.EntryKey] = state.UserNote;
        }
    }

    private string GetEntryNote(PackEntry entry)
    {
        string key = PackEntryUserStateService.BuildEntryKey(entry.PackId, entry.RelativePath);
        return _entryNoteLookup.TryGetValue(key, out string? note) ? note : string.Empty;
    }

    private bool IsPackEnabledForSelection(string packId)
    {
        return _userStateService.IsEnabledForSelection(packId);
    }

    private Texture2D? LoadThumbnailTextureCached(PackEntry entry)
    {
        if (_thumbnailCache.TryGetValue(entry.ThumbnailCacheKey, out Texture2D? cached))
        {
            return cached;
        }

        Texture2D? texture = LoadThumbnailTexture(entry);
        _thumbnailCache[entry.ThumbnailCacheKey] = texture;
        return texture;
    }

    private Texture2D? LoadPreviewTextureCached(PackEntry entry)
    {
        string cacheKey = $"{entry.ThumbnailCacheKey}|preview";
        if (_previewTextureCache.TryGetValue(cacheKey, out Texture2D? cached))
        {
            return cached;
        }

        Texture2D? texture = LoadTexture(entry.AbsolutePath);
        _previewTextureCache[cacheKey] = texture;
        return texture;
    }

    private static Texture2D? LoadThumbnailTexture(PackEntry entry)
    {
        try
        {
            Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            if (image.Load(entry.AbsolutePath) != Error.Ok)
            {
                return null;
            }

            image.Resize(92, 92, Image.Interpolation.Lanczos);
            return ImageTexture.CreateFromImage(image);
        }
        catch
        {
            return null;
        }
    }

    private static Texture2D? LoadTexture(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            if (image.Load(filePath) != Error.Ok)
            {
                return null;
            }

            return ImageTexture.CreateFromImage(image);
        }
        catch
        {
            return null;
        }
    }

    private sealed record PickerEntryItem(PackDescriptor Descriptor, PackEntry Entry);
}
