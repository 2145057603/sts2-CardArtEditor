using Godot;
using System;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class TopBarView : PanelContainer
{
    public event Action<string>? SearchChanged;
    public event Action? HistoryClicked;
    public event Action? BatchTaskClicked;
    public event Action? PackBrowserClicked;
    public event Action? PickerClicked;
    public event Action? FilterClicked;
    public event Action? SettingsClicked;
    public event Action? AboutClicked;
    public event Action? CloseClicked;

    private LineEdit? _searchInput;

    public override void _Ready()
    {
        Modulate = new Color(0.82f, 0.95f, 1.0f, 0.72f);

        AddChild(new ColorRect
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = new Color(0.07f, 0.13f, 0.17f, 0.44f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        HBoxContainer layout = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 12.0f,
            OffsetTop = 6.0f,
            OffsetRight = -12.0f,
            OffsetBottom = -6.0f
        };
        layout.AddThemeConstantOverride("separation", 8);
        AddChild(layout);

        layout.AddChild(new Label
        {
            Text = "卡面管理器",
            CustomMinimumSize = new Vector2(140.0f, 30.0f),
            VerticalAlignment = VerticalAlignment.Center
        });

        // UIV2 主界面现在只保留稳定的单卡工作流，旧批量入口从顶栏彻底移除，
        // 避免再和发布包一键替换、选图栏流程混在一起。
        layout.AddChild(new Label
        {
            Text = "单卡替换",
            CustomMinimumSize = new Vector2(92.0f, 30.0f),
            VerticalAlignment = VerticalAlignment.Center
        });

        Control spacer = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        layout.AddChild(spacer);

        _searchInput = new LineEdit
        {
            PlaceholderText = "搜索标题、类名或完整类型名",
            ClearButtonEnabled = true,
            CustomMinimumSize = new Vector2(320.0f, 30.0f)
        };
        _searchInput.TextChanged += text => SearchChanged?.Invoke(text);
        layout.AddChild(_searchInput);

        layout.AddChild(CreateButton("历史", () => HistoryClicked?.Invoke()));
        layout.AddChild(CreateButton("批量任务", () => BatchTaskClicked?.Invoke()));
        layout.AddChild(CreateButton("卡包", () => PackBrowserClicked?.Invoke()));
        layout.AddChild(CreateButton("选图栏", () => PickerClicked?.Invoke()));
        layout.AddChild(CreateButton("筛选", () => FilterClicked?.Invoke()));
        layout.AddChild(CreateButton("设置", () => SettingsClicked?.Invoke()));
        layout.AddChild(CreateButton("关于", () => AboutClicked?.Invoke()));
        layout.AddChild(CreateButton("关闭", () => CloseClicked?.Invoke()));
    }

    private static Button CreateButton(string text, Action onPressed)
    {
        Button button = new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(92.0f, 30.0f)
        };
        button.Pressed += onPressed;
        return button;
    }
}
