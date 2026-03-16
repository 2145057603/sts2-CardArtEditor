using Godot;
using InfectionCardReplacer.UIV2.Batch.Models;
using System;
using System.Collections.Generic;

namespace InfectionCardReplacer.UIV2.Batch.Views;

internal sealed partial class BatchTaskEntryTableView : PanelContainer
{
    public event Action<int>? EntrySelected;

    private readonly List<BatchTaskEntry> _entries = [];
    private readonly List<int> _visibleIndices = [];
    private LineEdit? _searchInput;
    private OptionButton? _statusFilter;
    private Label? _summaryLabel;
    private Tree? _tree;

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.06f, 0.08f, 0.10f, 0.92f),
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

        layout.AddChild(new Label { Text = "映射检查表" });

        HBoxContainer toolRow = new();
        toolRow.AddThemeConstantOverride("separation", 8);
        layout.AddChild(toolRow);

        _searchInput = new LineEdit
        {
            PlaceholderText = "搜索卡名、cardId、来源包",
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _searchInput.TextChanged += _ => RefreshVisibleItems();
        toolRow.AddChild(_searchInput);

        _statusFilter = new OptionButton();
        _statusFilter.AddItem("全部");
        _statusFilter.AddItem("仅已匹配");
        _statusFilter.AddItem("仅冲突");
        _statusFilter.AddItem("仅未匹配");
        _statusFilter.AddItem("仅已跳过");
        _statusFilter.ItemSelected += _ => RefreshVisibleItems();
        toolRow.AddChild(_statusFilter);

        _summaryLabel = new Label { Text = "总计 0 项" };
        layout.AddChild(_summaryLabel);

        _tree = new Tree
        {
            Columns = 6,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ColumnTitlesVisible = true
        };
        _tree.SetColumnTitle(0, "卡牌");
        _tree.SetColumnTitle(1, "角色");
        _tree.SetColumnTitle(2, "状态");
        _tree.SetColumnTitle(3, "来源包");
        _tree.SetColumnTitle(4, "匹配方式");
        _tree.SetColumnTitle(5, "候选图");
        _tree.ItemSelected += OnTreeItemSelected;
        layout.AddChild(_tree);
    }

    public void SetEntries(IReadOnlyList<BatchTaskEntry> entries, BatchTaskSummary summary, int selectedIndex)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        if (_summaryLabel is not null)
        {
            _summaryLabel.Text = $"总计 {summary.Total} | 已匹配 {summary.Matched} | 冲突 {summary.Conflict} | 未匹配 {summary.Missing} | 跳过 {summary.Skipped}";
        }

        RefreshVisibleItems();
        SelectEntry(selectedIndex);
    }

    public void SelectEntry(int entryIndex)
    {
        if (_tree is null || entryIndex < 0)
        {
            return;
        }

        TreeItem? item = _tree.GetRoot()?.GetFirstChild();
        while (item is not null)
        {
            Variant metadata = item.GetMetadata(0);
            if (metadata.VariantType == Variant.Type.Int && (int)metadata == entryIndex)
            {
                item.Select(0);
                return;
            }

            item = item.GetNext();
        }
    }

    private void RefreshVisibleItems()
    {
        if (_tree is null)
        {
            return;
        }

        _tree.Clear();
        TreeItem root = _tree.CreateItem();
        _visibleIndices.Clear();

        string keyword = _searchInput?.Text?.Trim() ?? string.Empty;
        int filterIndex = _statusFilter?.Selected ?? 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            BatchTaskEntry entry = _entries[i];
            if (!MatchesSearch(entry, keyword) || !MatchesStatus(entry, filterIndex))
            {
                continue;
            }

            _visibleIndices.Add(i);
            TreeItem item = _tree.CreateItem(root);
            item.SetText(0, entry.CardName);
            item.SetText(1, entry.RoleName);
            item.SetText(2, BuildStatusText(entry));
            item.SetText(3, string.IsNullOrWhiteSpace(entry.SourcePackName) ? "-" : entry.SourcePackName);
            item.SetText(4, entry.MatchReason);
            item.SetText(5, string.IsNullOrWhiteSpace(entry.CandidateRelativePath) ? "-" : entry.CandidateRelativePath);
            item.SetMetadata(0, i);
            if (entry.Status == BatchTaskEntryStatus.Conflict)
            {
                item.SetCustomColor(2, new Color(1.0f, 0.66f, 0.34f));
            }
            else if (entry.Status == BatchTaskEntryStatus.Missing)
            {
                item.SetCustomColor(2, new Color(1.0f, 0.44f, 0.44f));
            }
            else
            {
                item.SetCustomColor(2, new Color(0.58f, 1.0f, 0.68f));
            }
        }
    }

    private void OnTreeItemSelected()
    {
        if (_tree?.GetSelected() is not TreeItem item)
        {
            return;
        }

        Variant metadata = item.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
        {
            return;
        }

        EntrySelected?.Invoke((int)metadata);
    }

    private static bool MatchesSearch(BatchTaskEntry entry, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return entry.CardName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               entry.CardId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               entry.SourcePackName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesStatus(BatchTaskEntry entry, int filterIndex)
    {
        return filterIndex switch
        {
            1 => entry.Status is BatchTaskEntryStatus.Matched or BatchTaskEntryStatus.Manual,
            2 => entry.Status == BatchTaskEntryStatus.Conflict,
            3 => entry.Status == BatchTaskEntryStatus.Missing,
            4 => entry.IsSkipped,
            _ => true
        };
    }

    private static string BuildStatusText(BatchTaskEntry entry)
    {
        string baseText = entry.Status switch
        {
            BatchTaskEntryStatus.Matched => "已匹配",
            BatchTaskEntryStatus.Conflict => $"冲突 ({entry.ConflictCount})",
            BatchTaskEntryStatus.Manual => "人工修正",
            _ => "未匹配"
        };

        if (entry.IsSkipped)
        {
            return $"{baseText} / 已跳过";
        }

        if (entry.IsLocked)
        {
            return $"{baseText} / 已锁定";
        }

        return baseText;
    }
}
