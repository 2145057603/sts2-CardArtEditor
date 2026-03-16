using InfectionCardReplacer.UIV2;
using InfectionCardReplacer.UIV2.Batch.Models;
using InfectionCardReplacer.UIV2.Batch.Services;
using InfectionCardReplacer.UIV2.Batch.State;
using InfectionCardReplacer.UIV2.Batch.Views;
using InfectionCardReplacer.UIV2.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace InfectionCardReplacer.UIV2.Batch.Presenters;

internal sealed class BatchTaskPresenterRuntime
{
    private readonly BatchTaskState _state;
    private readonly SkinCenterFacade _facade;
    private readonly BatchTaskWindowShellView _window;
    private readonly BatchTaskSourceResolverViewService _sourceResolver = new();
    private readonly BatchCandidateMatchService _matchService = new();
    private readonly BatchTaskManualCandidateService _manualCandidateService = new();
    private readonly BatchTaskBulkCandidateService _bulkCandidateService = new();
    private readonly BatchTaskApplyService _applyService = new();
    private readonly Func<BatchTaskScope, IReadOnlyList<CardCatalogRegistry.Entry>> _resolveCards;
    private readonly Action _notifyReplacementsChanged;

    public BatchTaskPresenterRuntime(
        BatchTaskState state,
        SkinCenterFacade facade,
        BatchTaskWindowShellView window,
        Func<BatchTaskScope, IReadOnlyList<CardCatalogRegistry.Entry>> resolveCards,
        Action notifyReplacementsChanged)
    {
        _state = state;
        _facade = facade;
        _window = window;
        _resolveCards = resolveCards;
        _notifyReplacementsChanged = notifyReplacementsChanged;
    }

    public void Initialize()
    {
        _window.WindowOpened += RefreshSources;
        _window.SetupPanel.GenerateRequested += GenerateEntries;
        _window.SetupPanel.RefreshSourcesRequested += RefreshSources;
        _window.SetupPanel.SourceSelectionChanged += OnSourceSelectionChanged;
        _window.SetupPanel.SourcePreviewEntrySelected += AssignSourcePreviewToCurrentEntry;
        _window.EntryTable.EntrySelected += SelectEntry;
        _window.Inspector.ToggleLockRequested += ToggleLockForCurrent;
        _window.Inspector.ToggleSkipRequested += ToggleSkipForCurrent;
        _window.Inspector.ClearCandidateRequested += ClearCurrentCandidate;
        _window.BottomBar.ApplyRequested += ApplyConfirmedEntries;
        _window.BottomBar.ClearAllCandidatesRequested += ClearAllCandidates;
        _window.BottomBar.CloseRequested += () => _window.Hide();
    }

    public void RefreshSources()
    {
        _state.SourceOptions = _sourceResolver.GetSourceOptions();
        _window.SetupPanel.SetSourceOptions(_state.SourceOptions);
        UpdateSourcePreview();
        UpdateStatus("Batch sources refreshed");
        GenerateEntries(_window.SetupPanel.GetCurrentOptions());
    }

    private void GenerateEntries(BatchTaskGenerateOptions options)
    {
        _state.CurrentOptions = options;
        IReadOnlyList<CardCatalogRegistry.Entry> cards = _resolveCards(options.Scope);
        if (cards.Count == 0)
        {
            _state.Entries = [];
            _state.SelectedIndex = -1;
            RefreshAll("No cards found in current scope");
            return;
        }

        List<BatchTaskPackSource> sources = _sourceResolver.ResolveSources(options.SourceMode, options.SelectedPackId);
        if (sources.Count == 0)
        {
            _state.Entries = [];
            _state.SelectedIndex = -1;
            RefreshAll("No enabled pack source is available");
            return;
        }

        // Build candidates first and only apply after explicit user action.
        _state.Entries = _matchService.BuildEntries(cards, sources, options.MatchMode);
        _state.SelectedIndex = _state.Entries.Count > 0 ? 0 : -1;
        RefreshAll($"Generated {_state.Entries.Count} candidate mappings");
    }

    private void SelectEntry(int index)
    {
        if (index < 0 || index >= _state.Entries.Count)
        {
            return;
        }

        _state.SelectedIndex = index;
        RefreshInspector();
        UpdateStatus($"Current entry: {_state.Entries[index].CardName}");
    }

    private void ToggleLockForCurrent()
    {
        if (!TryGetSelectedEntry(out BatchTaskEntry? selectedEntry))
        {
            return;
        }

        BatchTaskEntry entry = selectedEntry;
        entry.IsLocked = !entry.IsLocked;
        RefreshAll(entry.IsLocked ? "Entry locked" : "Entry unlocked");
    }

    private void ToggleSkipForCurrent()
    {
        if (!TryGetSelectedEntry(out BatchTaskEntry? selectedEntry))
        {
            return;
        }

        BatchTaskEntry entry = selectedEntry;
        entry.IsSkipped = !entry.IsSkipped;
        RefreshAll(entry.IsSkipped ? "Entry skipped" : "Entry unskipped");
    }

    private void ClearCurrentCandidate()
    {
        if (!TryGetSelectedEntry(out BatchTaskEntry? selectedEntry))
        {
            return;
        }

        BatchTaskEntry entry = selectedEntry;
        entry.CandidateImagePath = string.Empty;
        entry.CandidateRelativePath = string.Empty;
        entry.SourcePackId = string.Empty;
        entry.SourcePackName = string.Empty;
        entry.ConflictCount = 0;
        entry.MatchReason = "Cleared";
        entry.Status = BatchTaskEntryStatus.Missing;
        RefreshAll("Current candidate cleared");
    }

    private void ApplyConfirmedEntries()
    {
        if (_state.Entries.Count == 0)
        {
            UpdateStatus("No batch entries to apply");
            return;
        }

        BatchTaskApplyResult result = _applyService.ApplyConfirmedEntries(_state.Entries);
        _notifyReplacementsChanged();
        RefreshAll(result.Message);
    }

    private void ClearAllCandidates()
    {
        if (_state.Entries.Count == 0)
        {
            UpdateStatus("当前没有可清空的候选。");
            return;
        }

        BatchTaskBulkCandidateClearResult result = _bulkCandidateService.ClearAllCandidates(_state.Entries);
        if (_state.SelectedIndex >= 0 &&
            _state.SelectedIndex < _state.Entries.Count &&
            string.IsNullOrWhiteSpace(_state.Entries[_state.SelectedIndex].CandidateImagePath))
        {
            // 当前选中项如果被一起清空，右侧预览要同步退回原卡图。
            RefreshAll(result.Message);
            return;
        }

        RefreshAll(result.Message);
    }

    private void RefreshAll(string status)
    {
        BatchTaskSummary summary = BuildSummary(_state.Entries);
        _window.EntryTable.SetEntries(_state.Entries, summary, _state.SelectedIndex);
        RefreshInspector();
        _window.BottomBar.SetStatus(summary, status);
        _state.StatusMessage = status;
    }

    private void RefreshInspector()
    {
        if (!TryGetSelectedEntry(out BatchTaskEntry? selectedEntry))
        {
            _window.Inspector.ShowEntry(null, new SkinCenterPreviewData());
            return;
        }

        BatchTaskEntry entry = selectedEntry;
        SkinCenterPreviewData preview = string.IsNullOrWhiteSpace(entry.CandidateImagePath)
            ? _facade.Preview.BuildPreviewData(entry.CardId)
            : _facade.Preview.BuildPreviewData(entry.CardId, entry.CandidateImagePath);
        _window.Inspector.ShowEntry(entry, preview);
    }

    private void UpdateStatus(string status)
    {
        BatchTaskSummary summary = BuildSummary(_state.Entries);
        _window.BottomBar.SetStatus(summary, status);
        _state.StatusMessage = status;
    }

    private void OnSourceSelectionChanged()
    {
        UpdateSourcePreview();
        GenerateEntries(_window.SetupPanel.GetCurrentOptions());
    }

    private void UpdateSourcePreview()
    {
        BatchTaskGenerateOptions options = _window.SetupPanel.GetCurrentOptions();
        List<BatchTaskPackSource> sources = _sourceResolver.ResolveSources(options.SourceMode, options.SelectedPackId);
        if (sources.Count == 0)
        {
            _window.SetupPanel.SetSourcePreviewEntries([], "No source images are available");
            return;
        }

        List<PackSystem.Models.PackEntry> previewEntries = sources
            .SelectMany(source => source.Index.Entries)
            .Take(60)
            .ToList();
        int totalCount = sources.Sum(source => source.Index.Entries.Count);
        string summary = $"Source packs: {sources.Count} | Images: {totalCount} | Showing first {previewEntries.Count}";
        _window.SetupPanel.SetSourcePreviewEntries(previewEntries, summary);
    }

    private void AssignSourcePreviewToCurrentEntry(PackSystem.Models.PackEntry sourceEntry)
    {
        BatchTaskEntry? currentEntry = null;
        if (_state.SelectedIndex >= 0 && _state.SelectedIndex < _state.Entries.Count)
        {
            currentEntry = _state.Entries[_state.SelectedIndex];
        }

        string sourcePackName = ResolveSourcePackName(sourceEntry.PackId);
        BatchTaskManualCandidateResult result = _manualCandidateService.TryAssignToEmptyEntry(
            currentEntry,
            sourceEntry,
            sourcePackName);

        if (!result.IsSuccess)
        {
            UpdateStatus(result.Message);
            return;
        }

        RefreshAll(result.Message);
    }

    private string ResolveSourcePackName(string packId)
    {
        BatchTaskSourceOption? sourceOption = _state.SourceOptions
            .FirstOrDefault(item => item.PackId.Equals(packId, StringComparison.OrdinalIgnoreCase));
        return sourceOption?.DisplayName ?? packId;
    }

    private bool TryGetSelectedEntry([NotNullWhen(true)] out BatchTaskEntry? entry)
    {
        if (_state.SelectedIndex < 0 || _state.SelectedIndex >= _state.Entries.Count)
        {
            entry = null;
            return false;
        }

        entry = _state.Entries[_state.SelectedIndex];
        return true;
    }

    private static BatchTaskSummary BuildSummary(IReadOnlyList<BatchTaskEntry> entries)
    {
        return new BatchTaskSummary
        {
            Total = entries.Count,
            Matched = entries.Count(item => item.Status == BatchTaskEntryStatus.Matched),
            Missing = entries.Count(item => item.Status == BatchTaskEntryStatus.Missing),
            Conflict = entries.Count(item => item.Status == BatchTaskEntryStatus.Conflict),
            Manual = entries.Count(item => item.Status == BatchTaskEntryStatus.Manual),
            Locked = entries.Count(item => item.IsLocked),
            Skipped = entries.Count(item => item.IsSkipped)
        };
    }
}
