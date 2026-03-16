using Godot;
using InfectionCardReplacer.UIV2.Batch.Models;
using System;

namespace InfectionCardReplacer.UIV2.Batch.Views;

internal sealed partial class BatchTaskBottomActionBarView : PanelContainer
{
    public event Action? ApplyRequested;
    public event Action? ClearAllCandidatesRequested;
    public event Action? CloseRequested;

    private Label? _statusLabel;

    public override void _Ready()
    {
        AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.08f, 0.10f, 0.13f, 0.95f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        HBoxContainer layout = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 12.0f,
            OffsetTop = 8.0f,
            OffsetRight = -12.0f,
            OffsetBottom = -8.0f
        };
        layout.AddThemeConstantOverride("separation", 8);
        AddChild(layout);

        _statusLabel = new Label
        {
            Text = "等待生成批量任务",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        layout.AddChild(_statusLabel);

        Button applyButton = new()
        {
            Text = "应用已确认映射",
            CustomMinimumSize = new Vector2(160.0f, 34.0f)
        };
        applyButton.Pressed += () => ApplyRequested?.Invoke();
        layout.AddChild(applyButton);

        Button clearAllCandidatesButton = new()
        {
            Text = "全部删除候选",
            CustomMinimumSize = new Vector2(150.0f, 34.0f)
        };
        clearAllCandidatesButton.Pressed += () => ClearAllCandidatesRequested?.Invoke();
        layout.AddChild(clearAllCandidatesButton);

        Button closeButton = new()
        {
            Text = "关闭",
            CustomMinimumSize = new Vector2(88.0f, 34.0f)
        };
        closeButton.Pressed += () => CloseRequested?.Invoke();
        layout.AddChild(closeButton);
    }

    public void SetStatus(BatchTaskSummary summary, string status)
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Text =
            $"{status} | 总计 {summary.Total} | 已匹配 {summary.Matched} | 冲突 {summary.Conflict} | 未匹配 {summary.Missing}";
    }
}
