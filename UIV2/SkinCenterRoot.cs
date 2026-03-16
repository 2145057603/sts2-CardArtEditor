using Godot;
using InfectionCardReplacer.PackSystem.Views;
using InfectionCardReplacer.UIV2.Batch.Models;
using InfectionCardReplacer.UIV2.Batch.Presenters;
using InfectionCardReplacer.UIV2.Batch.State;
using InfectionCardReplacer.UIV2.Batch.Views;
using InfectionCardReplacer.UIV2.Presenters;
using InfectionCardReplacer.UIV2.Services;
using InfectionCardReplacer.UIV2.State;
using InfectionCardReplacer.UIV2.Views;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InfectionCardReplacer.UIV2;

internal sealed partial class SkinCenterRoot : Control
{
    private static SkinCenterRoot? _instance;
    private PanelContainer? _backdrop;
    private SkinCenterPresenter? _presenter;
    private CleanSettingsDialogView? _settingsDialog;
    private PackBrowserWindow? _packBrowserWindow;
    private PackImagePickerDrawerView? _packImagePickerDrawer;
    private BatchTaskWindowShellView? _batchTaskWindow;
    private BatchTaskPresenterRuntime? _batchTaskPresenter;
    private int _lastSeenCatalogVersion = -1;
    private readonly SkinCenterFacade _facade = new();

    internal static void EnsureInstalledOnRoot()
    {
        if (_instance is not null && GodotObject.IsInstanceValid(_instance))
        {
            return;
        }

        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root is null)
        {
            return;
        }

        SkinCenterRoot root = new();
        tree.Root.CallDeferred(Node.MethodName.AddChild, root);
        _instance = root;
        GD.Print("[CardArtEditor][UIV2] SkinCenterRoot install requested.");
    }

    public override void _Ready()
    {
        Name = "SkinCenterRoot";
        AnchorRight = 1.0f;
        AnchorBottom = 1.0f;
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        _backdrop = new PanelContainer
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Visible = true,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.72f)
        };
        AddChild(_backdrop);

        _backdrop.AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.03f, 0.05f, 0.07f, 0.42f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        MarginContainer margin = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 24.0f,
            OffsetTop = 24.0f,
            OffsetRight = -24.0f,
            OffsetBottom = -24.0f
        };
        _backdrop.AddChild(margin);

        VBoxContainer shell = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f
        };
        shell.AddThemeConstantOverride("separation", 12);
        margin.AddChild(shell);

        TopBarView topBar = new()
        {
            CustomMinimumSize = new Vector2(0.0f, 42.0f)
        };
        shell.AddChild(topBar);

        HBoxContainer mainSplit = new()
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        mainSplit.AddThemeConstantOverride("separation", 12);
        shell.AddChild(mainSplit);

        ResourceListView resourceList = new()
        {
            CustomMinimumSize = new Vector2(320.0f, 0.0f),
            SizeFlagsHorizontal = Control.SizeFlags.Fill
        };
        mainSplit.AddChild(resourceList);

        PreviewView previewView = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        mainSplit.AddChild(previewView);

        DetailDrawerView detailDrawer = new()
        {
            CustomMinimumSize = new Vector2(360.0f, 0.0f),
            SizeFlagsHorizontal = Control.SizeFlags.Fill
        };
        mainSplit.AddChild(detailDrawer);

        BottomBarView bottomBar = new()
        {
            CustomMinimumSize = new Vector2(0.0f, 54.0f)
        };
        shell.AddChild(bottomBar);

        _settingsDialog = new CleanSettingsDialogView();
        AddChild(_settingsDialog);
        HistoryDialogView historyDialog = new();
        AddChild(historyDialog);
        FilterDialogView filterDialog = new();
        AddChild(filterDialog);
        AboutDialogView aboutDialog = new();
        AddChild(aboutDialog);
        // 卡包系统先接成独立弹窗，方便优先验证 UI 是否已经实际出现。
        _packBrowserWindow = new PackBrowserWindow();
        AddChild(_packBrowserWindow);
        _packImagePickerDrawer = new PackImagePickerDrawerView();
        AddChild(_packImagePickerDrawer);
        _batchTaskWindow = new BatchTaskWindowShellView();
        AddChild(_batchTaskWindow);

        FileDialog imageDialog = new()
        {
            Title = "选择图片",
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true
        };
        imageDialog.Filters = ["*.png, *.png", "*.jpg, *.jpg", "*.jpeg, *.jpeg", "*.webp, *.webp"];
        AddChild(imageDialog);

        _presenter = new SkinCenterPresenter(
            new SkinCenterState(),
            _facade,
            resourceList,
            previewView,
            detailDrawer,
            bottomBar);
        _batchTaskPresenter = new BatchTaskPresenterRuntime(
            new BatchTaskState(),
            _facade,
            _batchTaskWindow,
            ResolveBatchTaskCards,
            () => _presenter.RefreshAfterExternalReplacementChanges());

        topBar.SearchChanged += text => _presenter.RefreshCards(text);
        resourceList.CardSelected += index => _presenter.SelectCard(index);
        topBar.SettingsClicked += () => _settingsDialog?.PopupCentered();
        topBar.HistoryClicked += () =>
        {
            historyDialog.SetEntries(_facade.History.GetEntries());
            historyDialog.PopupCentered();
        };
        topBar.BatchTaskClicked += ToggleBatchTaskWindow;
        topBar.PackBrowserClicked += TogglePackBrowserWindow;
        topBar.PickerClicked += TogglePickerDrawer;
        _packBrowserWindow.UseImageRequested += path => _presenter.UsePackImageForSelectedCard(path, applyImmediately: false);
        _packBrowserWindow.ApplyImageRequested += path => _presenter.UsePackImageForSelectedCard(path, applyImmediately: true);
        _packImagePickerDrawer.UseImageRequested += path => _presenter.UsePackImageForSelectedCard(path, applyImmediately: false);
        _packImagePickerDrawer.ApplyImageRequested += path => _presenter.UsePackImageForSelectedCard(path, applyImmediately: true);
        _packImagePickerDrawer.ApplyReleasePackRequested += descriptor => _presenter.ApplyReleasePack(descriptor);
        topBar.FilterClicked += () =>
        {
            filterDialog.SetRoleItems(
                _facade.Cards.GetRoleSummaries(),
                _presenter.GetSelectedRoles(),
                _presenter.GetOnlyShowReplaced());
            filterDialog.PopupCentered();
        };
        topBar.AboutClicked += () => aboutDialog.PopupCentered();
        topBar.CloseClicked += () => Visible = false;
        detailDrawer.BrowseImageClicked += () => imageDialog.PopupCentered();
        detailDrawer.ApplyClicked += () => _presenter.ApplySelectedCardReplacement();
        detailDrawer.ResetClicked += () => _presenter.ResetSelectedCardReplacement();
        bottomBar.ApplyClicked += () => _presenter.ApplySelectedCardReplacement();
        bottomBar.ResetClicked += () => _presenter.ResetSelectedCardReplacement();
        bottomBar.ResetAllClicked += () => _presenter.ResetAllReplacements();
        imageDialog.FileSelected += path => _presenter.SetSelectedImagePath(path);
        filterDialog.FiltersChanged += (roles, onlyShowReplaced) => _presenter.UpdateFilters(roles, onlyShowReplaced);

        _presenter.Initialize();
        _batchTaskPresenter.Initialize();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        ShortcutSettingsService.EnsureLoaded();

        // 主界面开关使用可配置快捷键，默认仍然是 F7。
        if (ShortcutSettingsService.MatchesToggleShortcut(keyEvent))
        {
            Visible = !Visible;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (ShortcutSettingsService.MatchesPackBrowserShortcut(keyEvent))
        {
            TogglePackBrowserWindow();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (ShortcutSettingsService.MatchesPickerShortcut(keyEvent))
        {
            TogglePickerDrawer();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && ShortcutSettingsService.MatchesResetShortcut(keyEvent))
        {
            _presenter?.ResetSelectedCardReplacement();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && keyEvent.Keycode == Key.Escape)
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        CardCatalogRegistry.EnsureLoaded();

        if (GetTree()?.Root is Node root)
        {
            CardCatalogRegistry.TickScan(root, delta);
        }

        if (_presenter is null)
        {
            return;
        }

        int currentVersion = CardCatalogRegistry.Version;
        if (currentVersion == _lastSeenCatalogVersion)
        {
            return;
        }

        _lastSeenCatalogVersion = currentVersion;
        _presenter.RefreshIfCatalogChanged();
    }

    private void TogglePackBrowserWindow()
    {
        if (_packBrowserWindow is null)
        {
            return;
        }

        // 卡包浏览是主界面的工作窗口之一，所以快捷键打开它时也要确保主界面已经显示。
        Visible = true;
        if (_packBrowserWindow.Visible)
        {
            _packBrowserWindow.Hide();
            return;
        }

        _packBrowserWindow.PopupCentered();
    }

    private void TogglePickerDrawer()
    {
        if (_packImagePickerDrawer is null)
        {
            return;
        }

        // 选图栏依附在主界面右侧，快捷键切换时保持和主界面显隐一致。
        Visible = true;
        _packImagePickerDrawer.ToggleDrawer();
    }

    private void ToggleBatchTaskWindow()
    {
        if (_batchTaskWindow is null)
        {
            return;
        }

        Visible = true;
        if (_batchTaskWindow.Visible)
        {
            _batchTaskWindow.Hide();
            return;
        }

        _batchTaskWindow.PopupCentered();
    }

    private IReadOnlyList<CardCatalogRegistry.Entry> ResolveBatchTaskCards(BatchTaskScope scope)
    {
        return scope switch
        {
            BatchTaskScope.CurrentRole => ResolveCurrentRoleCards(),
            BatchTaskScope.AllCards => CardCatalogRegistry.GetEntries(),
            _ => ResolveCurrentFilteredCards()
        };
    }

    private IReadOnlyList<CardCatalogRegistry.Entry> ResolveCurrentFilteredCards()
    {
        if (_presenter is null)
        {
            return [];
        }

        Dictionary<string, CardCatalogRegistry.Entry> catalog = CardCatalogRegistry.GetEntries()
            .ToDictionary(item => item.FullTypeName, StringComparer.OrdinalIgnoreCase);
        return _presenter.GetFilteredCardsSnapshot()
            .Where(item => catalog.ContainsKey(item.CardId))
            .Select(item => catalog[item.CardId])
            .ToList();
    }

    private IReadOnlyList<CardCatalogRegistry.Entry> ResolveCurrentRoleCards()
    {
        if (_presenter is null)
        {
            return [];
        }

        string? role = null;
        SkinCenterCardItem? selectedCard = _presenter.GetSelectedCardSnapshot();
        if (selectedCard is not null)
        {
            role = CardCatalogRegistry.GetEntries()
                .FirstOrDefault(item => item.FullTypeName.Equals(selectedCard.CardId, StringComparison.OrdinalIgnoreCase))
                ?.RoleName;
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            IReadOnlyCollection<string> selectedRoles = _presenter.GetSelectedRoles();
            if (selectedRoles.Count == 1)
            {
                role = selectedRoles.First();
            }
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            return ResolveCurrentFilteredCards();
        }

        HashSet<string> roles = new([role], StringComparer.OrdinalIgnoreCase);
        return CardCatalogRegistry.GetEntries(selectedRoles: roles);
    }
}
