using Godot;
using InfectionCardReplacer.UIV2.Batch.Models;
using InfectionCardReplacer.UIV2;
using InfectionCardReplacer.UIV2.Views;
using System;

namespace InfectionCardReplacer.UIV2.Batch.Views;

internal sealed partial class BatchTaskInspectorView : PanelContainer
{
    public event Action? ToggleLockRequested;
    public event Action? ToggleSkipRequested;
    public event Action? ClearCandidateRequested;

    private Label? _cardInfoLabel;
    private Label? _matchInfoLabel;
    private Label? _pathInfoLabel;
    private Button? _lockButton;
    private Button? _skipButton;
    private PreviewView? _previewView;
    private BatchTaskEntry? _currentEntry;

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.07f, 0.09f, 0.12f, 0.94f),
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

        layout.AddChild(new Label { Text = "条目修正区" });
        _cardInfoLabel = new Label { Text = "当前未选择条目" };
        _matchInfoLabel = new Label { Text = "匹配信息：-" };
        _pathInfoLabel = new Label
        {
            Text = "候选路径：-",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        layout.AddChild(_cardInfoLabel);
        layout.AddChild(_matchInfoLabel);
        layout.AddChild(_pathInfoLabel);

        HBoxContainer buttonRow = new();
        buttonRow.AddThemeConstantOverride("separation", 8);
        layout.AddChild(buttonRow);

        _lockButton = new Button
        {
            Text = "锁定此条",
            CustomMinimumSize = new Vector2(0.0f, 32.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _lockButton.Pressed += () => ToggleLockRequested?.Invoke();
        buttonRow.AddChild(_lockButton);

        _skipButton = new Button
        {
            Text = "跳过此条",
            CustomMinimumSize = new Vector2(0.0f, 32.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _skipButton.Pressed += () => ToggleSkipRequested?.Invoke();
        buttonRow.AddChild(_skipButton);

        Button clearButton = new()
        {
            Text = "清空候选",
            CustomMinimumSize = new Vector2(0.0f, 32.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        clearButton.Pressed += () => ClearCandidateRequested?.Invoke();
        buttonRow.AddChild(clearButton);

        _previewView = new PreviewView
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0.0f, 520.0f)
        };
        layout.AddChild(_previewView);
    }

    public void ShowEntry(BatchTaskEntry? entry, SkinCenterPreviewData previewData)
    {
        _currentEntry = entry;
        if (entry is null)
        {
            _cardInfoLabel!.Text = "当前未选择条目";
            _matchInfoLabel!.Text = "匹配信息：-";
            _pathInfoLabel!.Text = "候选路径：-";
            _lockButton!.Text = "锁定此条";
            _skipButton!.Text = "跳过此条";
            _previewView?.ShowPreview(previewData);
            return;
        }

        _cardInfoLabel!.Text = $"卡牌：{entry.CardName} | {entry.RoleName} | {entry.CardType}";
        _matchInfoLabel!.Text = $"匹配信息：{entry.MatchReason}";
        _pathInfoLabel!.Text = $"候选路径：{(string.IsNullOrWhiteSpace(entry.CandidateImagePath) ? "-" : entry.CandidateImagePath)}";
        _lockButton!.Text = entry.IsLocked ? "取消锁定" : "锁定此条";
        _skipButton!.Text = entry.IsSkipped ? "取消跳过" : "跳过此条";
        _previewView?.ShowPreview(previewData);
    }
}
