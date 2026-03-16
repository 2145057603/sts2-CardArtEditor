using Godot;
using System;

namespace InfectionCardReplacer.UIV2.Batch.Views;

internal sealed partial class BatchTaskWindowShellView : Window
{
    private const float SetupPanelWidth = 430.0f;
    private const float InspectorPanelWidth = 560.0f;

    public event Action? WindowOpened;

    public BatchTaskSetupPanelView SetupPanel { get; private set; } = null!;
    public BatchTaskEntryTableView EntryTable { get; private set; } = null!;
    public BatchTaskInspectorView Inspector { get; private set; } = null!;
    public BatchTaskBottomActionBarView BottomBar { get; private set; } = null!;

    public override void _Ready()
    {
        Title = "批量任务中心";
        InitialPosition = WindowInitialPosition.CenterMainWindowScreen;
        // 恢复为普通弹窗宽度，只把高度适度加大，确保底部内容能完整显示。
        Size = new Vector2I(1660, 1020);
        Visible = false;
        CloseRequested += Hide;
        VisibilityChanged += () =>
        {
            if (Visible)
            {
                WindowOpened?.Invoke();
            }
        };

        VBoxContainer shell = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 14.0f,
            OffsetTop = 14.0f,
            OffsetRight = -14.0f,
            OffsetBottom = -14.0f
        };
        shell.AddThemeConstantOverride("separation", 10);
        AddChild(shell);

        HBoxContainer mainSplit = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        mainSplit.AddThemeConstantOverride("separation", 10);
        shell.AddChild(mainSplit);

        SetupPanel = new BatchTaskSetupPanelView
        {
            CustomMinimumSize = new Vector2(SetupPanelWidth, 0.0f),
            SizeFlagsHorizontal = Control.SizeFlags.Fill
        };
        mainSplit.AddChild(SetupPanel);

        EntryTable = new BatchTaskEntryTableView
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        mainSplit.AddChild(EntryTable);

        Inspector = new BatchTaskInspectorView
        {
            CustomMinimumSize = new Vector2(InspectorPanelWidth, 0.0f),
            SizeFlagsHorizontal = Control.SizeFlags.Fill
        };
        mainSplit.AddChild(Inspector);

        BottomBar = new BatchTaskBottomActionBarView
        {
            CustomMinimumSize = new Vector2(0.0f, 54.0f)
        };
        shell.AddChild(BottomBar);
    }
}
