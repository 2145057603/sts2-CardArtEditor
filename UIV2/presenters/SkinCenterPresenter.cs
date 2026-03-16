using InfectionCardReplacer.UIV2.Services;
using InfectionCardReplacer.PackSystem.Models;
using InfectionCardReplacer.PackSystem.Services;
using InfectionCardReplacer.UIV2.State;
using InfectionCardReplacer.UIV2.Views;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InfectionCardReplacer.UIV2.Presenters;

internal sealed class SkinCenterPresenter
{
    private int _lastCatalogVersion = -1;
    private readonly SkinCenterState _state;
    private readonly SkinCenterFacade _facade;
    private readonly ResourceListView _resourceListView;
    private readonly PreviewView _previewView;
    private readonly DetailDrawerView _detailDrawerView;
    private readonly BottomBarView _bottomBarView;
    private readonly ReleasePackApplyService _releasePackApplyService = new();
    private SkinCenterCardItem? _selectedCard;

    public SkinCenterPresenter(
        SkinCenterState state,
        SkinCenterFacade facade,
        ResourceListView resourceListView,
        PreviewView previewView,
        DetailDrawerView detailDrawerView,
        BottomBarView bottomBarView)
    {
        _state = state;
        _facade = facade;
        _resourceListView = resourceListView;
        _previewView = previewView;
        _detailDrawerView = detailDrawerView;
        _bottomBarView = bottomBarView;
    }

    public void Initialize()
    {
        RefreshCards(string.Empty, force: true);
        _detailDrawerView.Collapse();
        _bottomBarView.SetStatus("等待操作");
    }

    public void RefreshCards(string keyword, bool force = false)
    {
        if (!force && _lastCatalogVersion == CardCatalogRegistry.Version && string.Equals(_state.SearchKeyword, keyword, StringComparison.Ordinal))
        {
            return;
        }

        _state.SearchKeyword = keyword;
        _lastCatalogVersion = CardCatalogRegistry.Version;
        _state.FilteredCards = string.IsNullOrWhiteSpace(keyword)
            ? _facade.Cards.GetAllCards(_state.SelectedRoles, _state.OnlyShowReplaced)
            : _facade.Cards.SearchCards(keyword, _state.SelectedRoles, _state.OnlyShowReplaced);

        _resourceListView.SetSummary($"已收集 / 已选：{_state.FilteredCards.Count} / 0");
        _resourceListView.SetItems(_state.FilteredCards);
    }

    public void RefreshIfCatalogChanged()
    {
        RefreshCards(_state.SearchKeyword, force: false);
    }

    public IReadOnlyCollection<string> GetSelectedRoles()
    {
        return _state.SelectedRoles;
    }

    public IReadOnlyList<SkinCenterCardItem> GetFilteredCardsSnapshot()
    {
        return _state.FilteredCards.ToList();
    }

    public SkinCenterCardItem? GetSelectedCardSnapshot()
    {
        return _selectedCard;
    }

    public bool GetOnlyShowReplaced()
    {
        return _state.OnlyShowReplaced;
    }

    public void RefreshAfterExternalReplacementChanges()
    {
        if (_selectedCard is not null)
        {
            RefreshSelectedCardPreview();
            return;
        }

        RefreshCards(_state.SearchKeyword, force: true);
    }

    public void SelectCard(int index)
    {
        if (index < 0 || index >= _state.FilteredCards.Count)
        {
            return;
        }

        SkinCenterCardItem item = _state.FilteredCards[index];
        _selectedCard = item;
        _state.SelectedCardId = item.CardId;
        _state.PendingImagePath = string.Empty;
        SkinCenterPreviewData preview = _facade.Preview.BuildPreviewData(item.CardId);
        _previewView.ShowPreview(preview);
        _detailDrawerView.ShowCard(item, preview);
        _detailDrawerView.SetStatus(item.HasReplacement ? "当前卡牌已有替换图" : "当前卡牌使用原图");
        _bottomBarView.SetStatus($"当前已选中：{item.CardName}");
    }

    public void SetSelectedImagePath(string imagePath)
    {
        _state.PendingImagePath = imagePath ?? string.Empty;
        _detailDrawerView.SetImagePath(imagePath);
        if (_selectedCard is not null)
        {
            SkinCenterPreviewData preview = _facade.Preview.BuildPreviewData(_selectedCard.CardId, _state.PendingImagePath);
            _previewView.ShowPreview(preview);
            _detailDrawerView.ShowCard(_selectedCard, preview);
        }
        _detailDrawerView.SetStatus(string.IsNullOrWhiteSpace(imagePath) ? "尚未选择图片" : "已选择图片，等待应用");
        _bottomBarView.SetStatus(string.IsNullOrWhiteSpace(imagePath) ? "尚未选择图片" : "已选择图片，等待应用");
    }

    public string UsePackImageForSelectedCard(string imagePath, bool applyImmediately)
    {
        if (_selectedCard is null)
        {
            _bottomBarView.SetStatus("请先在左侧选择一张卡牌");
            _detailDrawerView.SetStatus("请先选择卡牌");
            return "请先在主界面左侧选择一张卡牌";
        }

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return "当前图片路径无效";
        }

        SetSelectedImagePath(imagePath);
        if (!applyImmediately)
        {
            return $"已将卡包图片回填到当前卡：{_selectedCard.CardName}";
        }

        ApplySelectedCardReplacement();
        return $"已直接应用到当前卡：{_selectedCard.CardName}";
    }

    public void ApplySelectedCardReplacement()
    {
        if (_selectedCard is null)
        {
            _bottomBarView.SetStatus("请先在左侧选择一张卡牌");
            _detailDrawerView.SetStatus("请先选择卡牌");
            return;
        }

        string imagePath = _detailDrawerView.GetImagePath();
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            _bottomBarView.SetStatus("请先选择一张图片");
            _detailDrawerView.SetStatus("请先选择图片");
            return;
        }

        string result = _facade.Replace.ApplyReplacement(_selectedCard.CardId, imagePath);
        _state.PendingImagePath = string.Empty;
        RefreshSelectedCardPreview();
        _detailDrawerView.SetStatus(result);
        _bottomBarView.SetStatus(result);
    }

    public void ResetSelectedCardReplacement()
    {
        if (_selectedCard is null)
        {
            _bottomBarView.SetStatus("请先在左侧选择一张卡牌");
            _detailDrawerView.SetStatus("请先选择卡牌");
            return;
        }

        string result = _facade.Replace.ResetReplacement(_selectedCard.CardId);
        _state.PendingImagePath = string.Empty;
        RefreshSelectedCardPreview(clearImagePath: true);
        _detailDrawerView.SetStatus(result);
        _bottomBarView.SetStatus(result);
    }

    public void ResetAllReplacements()
    {
        string result = _facade.Replace.ResetAllReplacements();
        _state.PendingImagePath = string.Empty;

        if (_selectedCard is not null)
        {
            RefreshSelectedCardPreview(clearImagePath: true);
            _detailDrawerView.SetStatus(result);
        }
        else
        {
            RefreshCards(_state.SearchKeyword, force: true);
        }

        _bottomBarView.SetStatus(result);
    }

    public void UpdateFilters(IReadOnlyCollection<string> roles, bool onlyShowReplaced)
    {
        _state.SelectedRoles = roles.Count == 0
            ? []
            : new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        _state.OnlyShowReplaced = onlyShowReplaced;

        RefreshCards(_state.SearchKeyword, force: true);

        string roleText = roles.Count == 0 ? "全部角色" : $"已筛选 {roles.Count} 个角色";
        string replacedText = onlyShowReplaced ? "，仅显示已替换" : string.Empty;
        _bottomBarView.SetStatus($"筛选已更新：{roleText}{replacedText}");
    }

    public string ApplyReleasePack(PackDescriptor descriptor)
    {
        ReleasePackApplyResult result = _releasePackApplyService.ApplyImportedReleasePack(descriptor);
        if (!result.IsSuccess)
        {
            _detailDrawerView.SetStatus(result.Message);
            _bottomBarView.SetStatus(result.Message);
            return result.Message;
        }

        // 整包替换完成后主动刷新当前卡和列表，避免界面停留在旧状态。
        if (_selectedCard is not null)
        {
            RefreshSelectedCardPreview();
        }
        else
        {
            RefreshCards(_state.SearchKeyword, force: true);
        }

        _detailDrawerView.SetStatus(result.Message);
        _bottomBarView.SetStatus(result.Message);
        return result.Message;
    }

    private void RefreshSelectedCardPreview(bool clearImagePath = false)
    {
        if (_selectedCard is null)
        {
            return;
        }

        SkinCenterPreviewData preview = _facade.Preview.BuildPreviewData(_selectedCard.CardId);
        if (clearImagePath)
        {
            preview.ReplacementPath = string.Empty;
        }

        _selectedCard.HasReplacement = preview.ReplacementTexture is not null;
        _previewView.ShowPreview(preview);
        _detailDrawerView.ShowCard(_selectedCard, preview);
        RefreshCards(_state.SearchKeyword, force: true);
    }
}
