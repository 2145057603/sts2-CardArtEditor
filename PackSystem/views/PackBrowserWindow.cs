using Godot;
using InfectionCardReplacer.PackSystem.Models;
using InfectionCardReplacer.PackSystem.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InfectionCardReplacer.PackSystem.Views;

internal sealed partial class PackBrowserWindow : Window
{
    public event Func<string, string>? UseImageRequested;
    public event Func<string, string>? ApplyImageRequested;

    private const int EntryColumnWidth = 150;

    private readonly PackRegistryService _registryService = new();
    private readonly PackIndexBuilder _indexBuilder = new();
    private readonly FlexibleZipPackImportService _zipPackImportService = new();
    private readonly ReleasePackImportService _releasePackImportService = new();
    private readonly ReleasePackExportService _releasePackExportService = new();
    private readonly ReleasePackApplyService _releasePackApplyService = new();
    private readonly PackMaintenanceService _packMaintenanceService = new();
    private readonly UserPackStateService _userStateService = new();
    private readonly InstalledPackStateService _installedPackStateService = new();
    private readonly PackEntryUserStateService _packEntryUserStateService = new();
    private readonly Dictionary<string, PackIndex> _packIndexCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D?> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D?> _previewTextureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _entryNoteLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly LineEdit _entryNoteInput = new();
    private readonly LineEdit _packNoteInput = new();

    private readonly List<PackDescriptor> _visiblePacks = [];
    private readonly List<PackEntry> _visibleEntries = [];

    private LineEdit? _packSearchInput;
    private ItemList? _packList;
    private Label? _packSummaryLabel;
    private Label? _packDetailLabel;
    private LineEdit? _entrySearchInput;
    private ItemList? _entryList;
    private Label? _entrySummaryLabel;
    private TextureRect? _previewTexture;
    private Label? _previewInfoLabel;
    private Label? _statusLabel;
    private FileDialog? _assetImportDialog;
    private FileDialog? _releaseImportDialog;
    private FileDialog? _exportReleaseDialog;
    private PopupMenu? _contextMenu;
    private ConfirmationDialog? _entryNoteDialog;
    private ConfirmationDialog? _packNoteDialog;
    private ConfirmationDialog? _deletePackDialog;
    private PackIndex? _currentIndex;
    private string? _currentPackId;
    private PackEntry? _selectedEntry;
    private PackEntry? _contextEntry;
    private PackDescriptor? _contextPack;

    private enum ContextAction
    {
        ClosePage = 1,
        EditEntryNote = 2
        ,
        EditPackNote = 3,
        DeletePack = 4,
        TogglePackEnabled = 5
    }

    public override void _Ready()
    {
        Title = "卡包浏览";
        InitialPosition = WindowInitialPosition.CenterMainWindowScreen;
        Size = new Vector2I(1480, 880);
        Visible = false;
        CloseRequested += Hide;
        VisibilityChanged += OnVisibilityChanged;

        VBoxContainer layout = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 16.0f,
            OffsetTop = 16.0f,
            OffsetRight = -16.0f,
            OffsetBottom = -16.0f
        };
        layout.AddThemeConstantOverride("separation", 10);
        layout.GuiInput += @event => OnWindowGuiInput(layout, @event);
        AddChild(layout);

        HBoxContainer toolbar = new();
        toolbar.AddThemeConstantOverride("separation", 8);
        layout.AddChild(toolbar);

        Button importAssetButton = new()
        {
            Text = "导入素材包",
            CustomMinimumSize = new Vector2(126.0f, 34.0f)
        };
        importAssetButton.Pressed += () => _assetImportDialog?.PopupCenteredRatio(0.75f);
        toolbar.AddChild(importAssetButton);

        Button importReleaseButton = new()
        {
            Text = "导入发布包",
            CustomMinimumSize = new Vector2(126.0f, 34.0f)
        };
        importReleaseButton.Pressed += () => _releaseImportDialog?.PopupCenteredRatio(0.75f);
        toolbar.AddChild(importReleaseButton);

        Button refreshButton = new()
        {
            Text = "刷新卡包",
            CustomMinimumSize = new Vector2(120.0f, 34.0f)
        };
        refreshButton.Pressed += ForceRefreshPacks;
        toolbar.AddChild(refreshButton);

        Button exportButton = new()
        {
            Text = "导出发布包",
            CustomMinimumSize = new Vector2(126.0f, 34.0f)
        };
        exportButton.Pressed += OpenExportDialog;
        toolbar.AddChild(exportButton);

        _packSearchInput = new LineEdit
        {
            PlaceholderText = "搜索卡包名称、作者、标签或备注",
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _packSearchInput.TextChanged += _ => RefreshPacks();
        toolbar.AddChild(_packSearchInput);

        _statusLabel = new Label
        {
            Text = "状态：等待导入或浏览卡包",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        layout.AddChild(_statusLabel);

        HBoxContainer mainSplit = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        mainSplit.AddThemeConstantOverride("separation", 10);
        layout.AddChild(mainSplit);

        mainSplit.AddChild(BuildPackColumn());
        mainSplit.AddChild(BuildEntryColumn());
        mainSplit.AddChild(BuildPreviewColumn());

        _assetImportDialog = new FileDialog
        {
            Title = "选择素材包 ZIP",
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true
        };
        _assetImportDialog.Filters = ["*.zip, *.zip"];
        _assetImportDialog.FileSelected += ImportAssetPack;
        AddChild(_assetImportDialog);

        _releaseImportDialog = new FileDialog
        {
            Title = "选择发布包 ZIP",
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true
        };
        _releaseImportDialog.Filters = ["*.zip, *.zip"];
        _releaseImportDialog.FileSelected += ImportReleasePack;
        AddChild(_releaseImportDialog);

        _exportReleaseDialog = new FileDialog
        {
            Title = "导出当前发布包",
            FileMode = FileDialog.FileModeEnum.SaveFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true
        };
        _exportReleaseDialog.Filters = ["*.zip, *.zip"];
        _exportReleaseDialog.FileSelected += ExportCurrentPack;
        AddChild(_exportReleaseDialog);

        _contextMenu = new PopupMenu();
        _contextMenu.IdPressed += OnContextMenuPressed;
        AddChild(_contextMenu);

        _entryNoteDialog = new ConfirmationDialog
        {
            Title = "图片备注",
            MinSize = new Vector2I(460, 180)
        };
        _entryNoteDialog.Confirmed += SaveSelectedEntryNoteFromDialog;
        _entryNoteInput.PlaceholderText = "填写当前图片备注";
        _entryNoteInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _entryNoteDialog.AddChild(new MarginContainer
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 12.0f,
            OffsetTop = 12.0f,
            OffsetRight = -12.0f,
            OffsetBottom = -12.0f
        });
        if (_entryNoteDialog.GetChildCount() > 0 && _entryNoteDialog.GetChild(0) is MarginContainer noteMargin)
        {
            VBoxContainer noteLayout = new();
            noteLayout.AddThemeConstantOverride("separation", 8);
            noteMargin.AddChild(noteLayout);
            noteLayout.AddChild(new Label { Text = "右键图片后可在这里编辑备注" });
            noteLayout.AddChild(_entryNoteInput);
        }
        AddChild(_entryNoteDialog);

        _packNoteDialog = new ConfirmationDialog
        {
            Title = "卡包备注",
            MinSize = new Vector2I(460, 180)
        };
        _packNoteDialog.Confirmed += SaveCurrentPackNoteFromDialog;
        _packNoteInput.PlaceholderText = "填写当前卡包备注";
        _packNoteInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _packNoteDialog.AddChild(new MarginContainer
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 12.0f,
            OffsetTop = 12.0f,
            OffsetRight = -12.0f,
            OffsetBottom = -12.0f
        });
        if (_packNoteDialog.GetChildCount() > 0 && _packNoteDialog.GetChild(0) is MarginContainer packNoteMargin)
        {
            VBoxContainer packNoteLayout = new();
            packNoteLayout.AddThemeConstantOverride("separation", 8);
            packNoteMargin.AddChild(packNoteLayout);
            packNoteLayout.AddChild(new Label { Text = "右键卡包后可在这里编辑备注" });
            packNoteLayout.AddChild(_packNoteInput);
        }
        AddChild(_packNoteDialog);

        _deletePackDialog = new ConfirmationDialog
        {
            Title = "确认删除卡包",
            DialogText = "确认删除当前卡包吗？此操作会删除 Imported 包的安装目录。"
        };
        _deletePackDialog.Confirmed += DeleteCurrentPackConfirmed;
        AddChild(_deletePackDialog);
    }

    private void OnVisibilityChanged()
    {
        // 卡包窗口关闭后节点并不会销毁，所以再次打开时尽量复用现成列表和缩略图，
        // 避免每次都对 300+ 图片重新扫目录、重新解码缩略图。
        if (Visible && (_packList?.ItemCount ?? 0) == 0)
        {
            RefreshPacks();
        }
    }

    public void RefreshPacks(string? preferredPackId = null)
    {
        IReadOnlyList<PackDescriptor> packs = _registryService.ScanAvailablePacks(includeLooseFolder: true);
        string keyword = _packSearchInput?.Text?.Trim() ?? string.Empty;

        _visiblePacks.Clear();
        foreach (PackDescriptor pack in packs)
        {
            UserPackState? state = _userStateService.GetByPackId(pack.PackId);
            if (!MatchesPackKeyword(pack, state, keyword))
            {
                continue;
            }

            _visiblePacks.Add(pack);
        }

        if (_packSummaryLabel is not null)
        {
            _packSummaryLabel.Text = $"卡包数量：{_visiblePacks.Count}";
        }

        if (_packList is not null)
        {
            _packList.Clear();
            foreach (PackDescriptor pack in _visiblePacks)
            {
                UserPackState? state = _userStateService.GetByPackId(pack.PackId);
                string degradedText = pack.IsDegraded ? " [降级]" : string.Empty;
                string authorText = string.IsNullOrWhiteSpace(pack.Author) ? "未知作者" : pack.Author;
                string enabledText = IsPackEnabledForSelection(pack.PackId) ? "已启用" : "未启用";
                string noteText = string.IsNullOrWhiteSpace(state?.UserNote) ? string.Empty : $" | 备注：{state!.UserNote}";
                _packList.AddItem($"{pack.DisplayName} ({pack.Version}){degradedText}\n{authorText} | {pack.SourceType} | {enabledText}{noteText}");
                int itemIndex = _packList.ItemCount - 1;
                if (ReleasePackDescriptorHelper.IsReleasePack(pack))
                {
                    // 发布包在卡包列表里直接用粉色高亮，方便和普通素材包快速区分。
                    _packList.SetItemCustomFgColor(itemIndex, new Color(1.0f, 0.48f, 0.78f, 1.0f));
                }
            }
        }

        if (_visiblePacks.Count == 0)
        {
            ClearCurrentPack();
            return;
        }

        if (_packList is not null)
        {
            string? targetPackId = !string.IsNullOrWhiteSpace(preferredPackId) ? preferredPackId : _currentPackId;
            int selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(targetPackId))
            {
                int preferredIndex = _visiblePacks.FindIndex(pack => pack.PackId.Equals(targetPackId, StringComparison.OrdinalIgnoreCase));
                if (preferredIndex >= 0)
                {
                    selectedIndex = preferredIndex;
                }
            }

            _packList.Select(selectedIndex);
            ShowSelectedPack(selectedIndex);
        }
    }

    private void ImportAssetPack(string zipPath)
    {
        SetStatus($"状态：正在导入素材包 {Path.GetFileName(zipPath)} ...", false);
        ZipPackImportResult result = _zipPackImportService.ImportFromZip(zipPath);
        SetStatus($"状态：{result.Message}", !result.IsSuccess);

        if (result.IsSuccess)
        {
            ClearPackCaches(result.PackId);
            RefreshPacks(result.PackId);
        }
    }

    private void ImportReleasePack(string zipPath)
    {
        SetStatus($"状态：正在导入发布包 {Path.GetFileName(zipPath)} ...", false);
        ZipPackImportResult result = _releasePackImportService.ImportReleasePack(zipPath);
        if (!result.IsSuccess)
        {
            SetStatus($"状态：{result.Message}", true);
            return;
        }

        ClearPackCaches(result.PackId);
        RefreshPacks(result.PackId);

        PackDescriptor? importedDescriptor = _registryService
            .ScanAvailablePacks(includeLooseFolder: true)
            .FirstOrDefault(pack => pack.PackId.Equals(result.PackId, StringComparison.OrdinalIgnoreCase));
        ReleasePackApplyResult applyResult = _releasePackApplyService.ApplyImportedReleasePack(importedDescriptor);
        if (applyResult.IsSuccess)
        {
            SetStatus($"状态：{result.Message} {applyResult.Message}", false);
            return;
        }

        SetStatus($"状态：{result.Message} 但自动应用失败：{applyResult.Message}", true);
    }

    private void OpenExportDialog()
    {
        if (_exportReleaseDialog is null)
        {
            SetStatus("状态：导出对话框未初始化。", true);
            return;
        }

        _exportReleaseDialog.CurrentFile = ReleasePackExportService.BuildSuggestedFileName();
        _exportReleaseDialog.PopupCenteredRatio(0.75f);
    }

    private void ExportCurrentPack(string destinationPath)
    {
        SetStatus("状态：正在从当前已替换结果导出发布包 ...", false);
        PackExportResult result = _releasePackExportService.ExportCurrentReplacements(destinationPath);
        SetStatus($"状态：{result.Message}", !result.IsSuccess);
    }

    private void ForceRefreshPacks()
    {
        _packIndexCache.Clear();
        _thumbnailCache.Clear();
        _previewTextureCache.Clear();
        RefreshPacks();
    }

    private Control BuildPackColumn()
    {
        VBoxContainer column = new()
        {
            CustomMinimumSize = new Vector2(330.0f, 0.0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        column.AddThemeConstantOverride("separation", 8);

        column.AddChild(new Label { Text = "卡包列表" });
        _packSummaryLabel = new Label { Text = "卡包数量：0" };
        column.AddChild(_packSummaryLabel);

        _packList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            AllowReselect = true
        };
        _packList.ItemSelected += ShowSelectedPack;
        _packList.GuiInput += OnPackListGuiInput;
        column.AddChild(_packList);

        return column;
    }

    private Control BuildEntryColumn()
    {
        VBoxContainer column = new()
        {
            CustomMinimumSize = new Vector2(520.0f, 0.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        column.AddThemeConstantOverride("separation", 8);

        column.AddChild(new Label { Text = "包内图片" });

        _entrySearchInput = new LineEdit
        {
            PlaceholderText = "搜索文件名、相对路径或图片备注",
            ClearButtonEnabled = true
        };
        _entrySearchInput.TextChanged += _ => RefreshEntries();
        column.AddChild(_entrySearchInput);

        _entrySummaryLabel = new Label { Text = "图片数量：0" };
        column.AddChild(_entrySummaryLabel);

        _entryList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            IconMode = ItemList.IconModeEnum.Top,
            FixedColumnWidth = EntryColumnWidth,
            SameColumnWidth = true,
            FixedIconSize = new Vector2I(96, 96),
            MaxTextLines = 2
        };
        _entryList.Resized += UpdateEntryGridLayout;
        _entryList.ItemSelected += ShowSelectedEntry;
        _entryList.GuiInput += OnEntryListGuiInput;
        column.AddChild(_entryList);

        return column;
    }

    private Control BuildPreviewColumn()
    {
        VBoxContainer column = new()
        {
            CustomMinimumSize = new Vector2(360.0f, 0.0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        column.AddThemeConstantOverride("separation", 8);

        column.AddChild(new Label { Text = "详情预览" });

        _packDetailLabel = new Label
        {
            Text = "请选择卡包",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        column.AddChild(_packDetailLabel);

        PanelContainer previewPanel = new()
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        column.AddChild(previewPanel);

        CenterContainer center = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f
        };
        previewPanel.AddChild(center);

        _previewTexture = new TextureRect
        {
            CustomMinimumSize = new Vector2(320.0f, 420.0f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        center.AddChild(_previewTexture);

        _previewInfoLabel = new Label
        {
            Text = "请选择图片",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        column.AddChild(_previewInfoLabel);

        HBoxContainer actionRow = new();
        actionRow.AddThemeConstantOverride("separation", 8);
        column.AddChild(actionRow);

        Button useButton = new()
        {
            Text = "设为当前卡",
            CustomMinimumSize = new Vector2(128.0f, 36.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        useButton.Pressed += UseSelectedImageForCurrentCard;
        actionRow.AddChild(useButton);

        Button applyButton = new()
        {
            Text = "直接应用",
            CustomMinimumSize = new Vector2(128.0f, 36.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        applyButton.Pressed += ApplySelectedImageToCurrentCard;
        actionRow.AddChild(applyButton);

        return column;
    }

    private void ShowSelectedPack(long selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= _visiblePacks.Count)
        {
            ClearCurrentPack();
            return;
        }

        PackDescriptor descriptor = _visiblePacks[(int)selectedIndex];
        _currentPackId = descriptor.PackId;

        string cacheKey = BuildPackCacheKey(descriptor);
        if (!_packIndexCache.TryGetValue(cacheKey, out PackIndex? cachedIndex))
        {
            cachedIndex = _indexBuilder.BuildIndex(descriptor);
            if (cachedIndex is not null)
            {
                _packIndexCache[cacheKey] = cachedIndex;
            }
        }

        _currentIndex = cachedIndex;
        UserPackState? state = _userStateService.GetByPackId(descriptor.PackId);
        InstalledPackState? installedState = _installedPackStateService.GetByInstance(descriptor.PackId, descriptor.SourceType, descriptor.RootPath);

        if (_packDetailLabel is not null)
        {
            string noteText = string.IsNullOrWhiteSpace(state?.UserNote) ? "无备注" : state!.UserNote;
            string degradedText = descriptor.IsDegraded ? "\n状态：映射不可用，当前按素材包降级浏览" : string.Empty;
            string installedStatusText = installedState is null ? "未记录" : installedState.Status.ToString();
            string warningText = string.IsNullOrWhiteSpace(installedState?.WarningMessage) ? string.Empty : $"\n警告：{installedState.WarningMessage}";
            string enabledText = IsPackEnabledForSelection(descriptor.PackId) ? "已启用" : "未启用";
            string releaseText = ReleasePackDescriptorHelper.IsReleasePack(descriptor) ? "是" : "否";
            _packDetailLabel.Text =
                $"名称：{descriptor.DisplayName}\n" +
                $"作者：{(string.IsNullOrWhiteSpace(descriptor.Author) ? "未知作者" : descriptor.Author)}\n" +
                $"版本：{descriptor.Version}\n" +
                $"来源：{descriptor.SourceType}\n" +
                $"类型：{descriptor.PackType}\n" +
                $"发布包：{releaseText}\n" +
                $"启用：{enabledText}\n" +
                $"状态：{installedStatusText}\n" +
                $"图片数：{cachedIndex?.Entries.Count ?? 0}\n" +
                $"备注：{noteText}{degradedText}{warningText}\n\n" +
                $"{descriptor.Description}";
        }

        RefreshEntries();
    }

    private void RefreshEntries()
    {
        _visibleEntries.Clear();
        _entryList?.Clear();
        _selectedEntry = null;
        ClearPreview();

        if (_currentIndex is null)
        {
            if (_entrySummaryLabel is not null)
            {
                _entrySummaryLabel.Text = "图片数量：0";
            }
            return;
        }

        ReloadEntryNoteLookup();
        string keyword = _entrySearchInput?.Text?.Trim() ?? string.Empty;
        foreach (PackEntry entry in _currentIndex.Entries)
        {
            if (!MatchesEntryKeyword(entry, keyword))
            {
                continue;
            }

            _visibleEntries.Add(entry);
        }

        if (_entrySummaryLabel is not null)
        {
            _entrySummaryLabel.Text = $"图片数量：{_visibleEntries.Count}";
        }

        if (_entryList is null)
        {
            return;
        }

        UpdateEntryGridLayout();
        foreach (PackEntry entry in _visibleEntries)
        {
            Texture2D? icon = LoadThumbnailTextureCached(entry);
            string subtitle = entry.Width > 0 && entry.Height > 0 ? $"{entry.Width}x{entry.Height}" : entry.RelativePath;
            _entryList.AddItem($"{entry.FileName}\n{subtitle}", icon);
        }

        if (_visibleEntries.Count > 0)
        {
            _entryList.Select(0);
            ShowSelectedEntry(0);
        }
    }

    private void ShowSelectedEntry(long selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= _visibleEntries.Count)
        {
            ClearPreview();
            return;
        }

        PackEntry entry = _visibleEntries[(int)selectedIndex];
        _selectedEntry = entry;
        Texture2D? texture = LoadPreviewTextureCached(entry);
        string entryNote = GetEntryNote(entry);

        if (_previewTexture is not null)
        {
            _previewTexture.Texture = texture;
        }

        if (_previewInfoLabel is not null)
        {
            _previewInfoLabel.Text =
                $"文件：{entry.FileName}\n" +
                $"路径：{entry.RelativePath}\n" +
                $"尺寸：{entry.Width}x{entry.Height}\n" +
                $"缓存键：{entry.ThumbnailCacheKey}\n" +
                $"图片备注：{(string.IsNullOrWhiteSpace(entryNote) ? "无" : entryNote)}";
        }
    }

    private void ClearCurrentPack()
    {
        _currentIndex = null;
        _visibleEntries.Clear();
        _selectedEntry = null;
        _entryList?.Clear();
        if (_packDetailLabel is not null)
        {
            _packDetailLabel.Text = "请选择卡包";
        }

        if (_entrySummaryLabel is not null)
        {
            _entrySummaryLabel.Text = "图片数量：0";
        }

        ClearPreview();
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
    }

    private static bool MatchesPackKeyword(PackDescriptor pack, UserPackState? state, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return pack.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               pack.Author.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               pack.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               pack.PackId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               pack.Tags.Any(tag => tag.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(state?.UserNote) && state.UserNote.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchesEntryKeyword(PackEntry entry, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return entry.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               entry.RelativePath.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               GetEntryNote(entry).Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static Texture2D? LoadThumbnailTexture(PackEntry entry)
    {
        try
        {
            // 当前先直接本地读取缩略图，后续再接正式缓存服务。
            Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            if (image.Load(entry.AbsolutePath) != Error.Ok)
            {
                return null;
            }

            image.Resize(96, 96, Image.Interpolation.Lanczos);
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
            // 这里保持原图加载，方便先验证图片浏览链路是否正确。
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

    private void ClearPackCaches(string packId)
    {
        foreach (string key in _packIndexCache.Keys.Where(key => key.StartsWith($"{packId}|", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _packIndexCache.Remove(key);
        }

        foreach (string key in _thumbnailCache.Keys.Where(key => key.StartsWith($"{packId}_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _thumbnailCache.Remove(key);
        }

        foreach (string key in _previewTextureCache.Keys.Where(key => key.StartsWith($"{packId}_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _previewTextureCache.Remove(key);
        }
    }

    private static string BuildPackCacheKey(PackDescriptor descriptor)
    {
        return $"{descriptor.PackId}|{descriptor.Version}|{descriptor.RootPath}";
    }

    private PackDescriptor? GetCurrentPackDescriptor()
    {
        if (string.IsNullOrWhiteSpace(_currentPackId))
        {
            return null;
        }

        return _visiblePacks.FirstOrDefault(pack => pack.PackId.Equals(_currentPackId, StringComparison.OrdinalIgnoreCase));
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

    private void UpdateEntryGridLayout()
    {
        if (_entryList is null)
        {
            return;
        }

        // 中栏列数按当前可用宽度动态计算，避免窗口放大后仍然被固定在 4/5 列。
        int columns = Math.Max(1, (int)Math.Floor(_entryList.Size.X / EntryColumnWidth));
        _entryList.MaxColumns = columns;
    }

    private void UseSelectedImageForCurrentCard()
    {
        if (_selectedEntry is null)
        {
            SetStatus("状态：请先在中间选择一张图片。", true);
            return;
        }

        if (UseImageRequested is null)
        {
            SetStatus("状态：当前窗口尚未接入主界面卡牌编辑流程。", true);
            return;
        }

        if (!IsCurrentPackEnabledForSelection())
        {
            SetStatus("状态：当前卡包未启用，请先在卡包列表中启用后再使用。", true);
            return;
        }

        string message = UseImageRequested.Invoke(_selectedEntry.AbsolutePath);
        SetStatus($"状态：{message}", false);
    }

    private void ApplySelectedImageToCurrentCard()
    {
        if (_selectedEntry is null)
        {
            SetStatus("状态：请先在中间选择一张图片。", true);
            return;
        }

        if (ApplyImageRequested is null)
        {
            SetStatus("状态：当前窗口尚未接入主界面卡牌编辑流程。", true);
            return;
        }

        if (!IsCurrentPackEnabledForSelection())
        {
            SetStatus("状态：当前卡包未启用，请先在卡包列表中启用后再应用。", true);
            return;
        }

        string message = ApplyImageRequested.Invoke(_selectedEntry.AbsolutePath);
        SetStatus($"状态：{message}", false);
    }

    private void SaveCurrentPackNoteFromDialog()
    {
        if (_contextPack is null)
        {
            SetStatus("状态：请先选择一个卡包。", true);
            return;
        }

        UserPackState state = _userStateService.GetByPackId(_contextPack.PackId) ?? new UserPackState
        {
            PackId = _contextPack.PackId
        };
        state.UserNote = _packNoteInput.Text?.Trim() ?? string.Empty;
        _userStateService.Upsert(state);
        RefreshPacks(_contextPack.PackId);
        SetStatus("状态：卡包备注已保存。", false);
    }

    private void SaveSelectedEntryNoteFromDialog()
    {
        if (_contextEntry is null)
        {
            SetStatus("状态：请先选择一张图片。", true);
            return;
        }

        string note = _entryNoteInput.Text?.Trim() ?? string.Empty;
        _packEntryUserStateService.Upsert(_contextEntry.PackId, _contextEntry.RelativePath, note);
        int selectedIndex = _visibleEntries.FindIndex(item =>
            item.PackId.Equals(_contextEntry.PackId, StringComparison.OrdinalIgnoreCase) &&
            item.RelativePath.Equals(_contextEntry.RelativePath, StringComparison.OrdinalIgnoreCase));
        if (selectedIndex >= 0)
        {
            ShowSelectedEntry(selectedIndex);
        }

        ReloadEntryNoteLookup();
        SetStatus(string.IsNullOrWhiteSpace(note) ? "状态：图片备注已清空。" : "状态：图片备注已保存。", false);
    }

    private void DeleteCurrentPack()
    {
        if (_contextPack is null)
        {
            SetStatus("状态：请先选择一个卡包。", true);
            return;
        }

        if (_deletePackDialog is null)
        {
            SetStatus("状态：删除确认框未初始化。", true);
            return;
        }

        _deletePackDialog.DialogText = $"确认删除卡包“{_contextPack.DisplayName}”吗？此操作仅对 Imported 卡包生效。";
        _deletePackDialog.PopupCentered();
    }

    private void DeleteCurrentPackConfirmed()
    {
        if (_contextPack is null)
        {
            SetStatus("状态：请先选择一个卡包。", true);
            return;
        }

        PackDeleteResult result = _packMaintenanceService.DeleteImportedPack(_contextPack);
        SetStatus($"状态：{result.Message}", !result.IsSuccess);

        if (!result.IsSuccess)
        {
            return;
        }

        ClearPackCaches(_contextPack.PackId);
        _currentPackId = null;
        _contextPack = null;
        RefreshPacks();
    }

    private void OnPackListGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseEvent ||
            !mouseEvent.Pressed ||
            mouseEvent.ButtonIndex != MouseButton.Right)
        {
            return;
        }

        if (_packList is null)
        {
            return;
        }

        int itemIndex = _packList.GetItemAtPosition(mouseEvent.Position, true);
        if (itemIndex < 0 || itemIndex >= _visiblePacks.Count)
        {
            ShowContextMenu(_packList, mouseEvent.Position, null, null);
            GetViewport()?.SetInputAsHandled();
            return;
        }

        _packList.Select(itemIndex);
        ShowSelectedPack(itemIndex);
        ShowContextMenu(_packList, mouseEvent.Position, _visiblePacks[itemIndex], null);
        GetViewport()?.SetInputAsHandled();
    }

    private void OnEntryListGuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseEvent ||
            !mouseEvent.Pressed ||
            mouseEvent.ButtonIndex != MouseButton.Right)
        {
            return;
        }

        if (_entryList is null)
        {
            return;
        }

        int itemIndex = _entryList.GetItemAtPosition(mouseEvent.Position, true);
        if (itemIndex < 0 || itemIndex >= _visibleEntries.Count)
        {
            ShowContextMenu(_entryList, mouseEvent.Position, null, null);
            GetViewport()?.SetInputAsHandled();
            return;
        }

        _entryList.Select(itemIndex);
        ShowSelectedEntry(itemIndex);
        ShowContextMenu(_entryList, mouseEvent.Position, null, _visibleEntries[itemIndex]);
        GetViewport()?.SetInputAsHandled();
    }

    private void OnWindowGuiInput(Control source, InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseEvent ||
            !mouseEvent.Pressed ||
            mouseEvent.ButtonIndex != MouseButton.Right)
        {
            return;
        }

        ShowContextMenu(source, mouseEvent.Position, null, null);
        GetViewport()?.SetInputAsHandled();
    }

    private void ShowContextMenu(Control source, Vector2 localMousePosition, PackDescriptor? pack, PackEntry? entry)
    {
        if (_contextMenu is null)
        {
            return;
        }

        _contextPack = pack;
        _contextEntry = entry;
        _contextMenu.Clear();
        if (pack is not null)
        {
            _contextMenu.AddItem(IsPackEnabledForSelection(pack.PackId) ? "禁用卡包" : "启用卡包", (int)ContextAction.TogglePackEnabled);
            _contextMenu.AddItem("卡包备注", (int)ContextAction.EditPackNote);
            if (pack.SourceType == PackSourceType.Imported)
            {
                _contextMenu.AddItem("删除卡包", (int)ContextAction.DeletePack);
            }
            _contextMenu.AddSeparator();
        }

        if (entry is not null)
        {
            _contextMenu.AddItem("图片备注", (int)ContextAction.EditEntryNote);
            _contextMenu.AddSeparator();
        }

        _contextMenu.AddItem("关闭页面", (int)ContextAction.ClosePage);
        // 这里改成基于控件局部坐标转窗口坐标，避免把屏幕坐标直接塞给 PopupMenu 后出现明显偏移。
        Vector2 popupPosition = source.GetGlobalRect().Position + localMousePosition;
        _contextMenu.Position = new Vector2I((int)Math.Round(popupPosition.X), (int)Math.Round(popupPosition.Y));
        _contextMenu.Popup();
    }

    private void OnContextMenuPressed(long id)
    {
        switch ((ContextAction)id)
        {
            case ContextAction.EditPackNote:
                OpenPackNoteDialog();
                break;
            case ContextAction.TogglePackEnabled:
                ToggleCurrentPackEnabled();
                break;
            case ContextAction.DeletePack:
                DeleteCurrentPack();
                break;
            case ContextAction.EditEntryNote:
                OpenEntryNoteDialog();
                break;
            case ContextAction.ClosePage:
                Hide();
                break;
        }
    }

    private void OpenEntryNoteDialog()
    {
        if (_contextEntry is null || _entryNoteDialog is null)
        {
            SetStatus("状态：请先右键一张图片。", true);
            return;
        }

        PackEntryUserState? state = _packEntryUserStateService.Get(_contextEntry.PackId, _contextEntry.RelativePath);
        _entryNoteInput.Text = state?.UserNote ?? string.Empty;
        _entryNoteDialog.PopupCentered();
    }

    private void OpenPackNoteDialog()
    {
        if (_contextPack is null || _packNoteDialog is null)
        {
            SetStatus("状态：请先右键一个卡包。", true);
            return;
        }

        UserPackState? state = _userStateService.GetByPackId(_contextPack.PackId);
        _packNoteInput.Text = state?.UserNote ?? string.Empty;
        _packNoteDialog.PopupCentered();
    }

    private void ToggleCurrentPackEnabled()
    {
        if (_contextPack is null)
        {
            SetStatus("状态：请先右键一个卡包。", true);
            return;
        }

        bool nextEnabled = !IsPackEnabledForSelection(_contextPack.PackId);
        _userStateService.SetEnabledForSelection(_contextPack.PackId, nextEnabled);
        RefreshPacks(_contextPack.PackId);
        SetStatus(nextEnabled ? "状态：卡包已启用，可参与选图和应用。" : "状态：卡包已禁用，不再参与选图和应用。", false);
    }

    private bool IsCurrentPackEnabledForSelection()
    {
        return !string.IsNullOrWhiteSpace(_currentPackId) && IsPackEnabledForSelection(_currentPackId);
    }

    private bool IsPackEnabledForSelection(string packId)
    {
        return _userStateService.IsEnabledForSelection(packId);
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
}
