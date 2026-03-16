using Godot;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class AboutDialogView : Window
{
    public override void _Ready()
    {
        Title = "关于";
        InitialPosition = WindowInitialPosition.CenterMainWindowScreen;
        Size = new Vector2I(520, 360);
        Visible = false;
        CloseRequested += Hide;

        VBoxContainer layout = new()
        {
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = 20.0f,
            OffsetTop = 20.0f,
            OffsetRight = -20.0f,
            OffsetBottom = -20.0f
        };
        layout.AddThemeConstantOverride("separation", 10);
        AddChild(layout);

        layout.AddChild(new Label { Text = "卡面管理器" });
        layout.AddChild(new Label { Text = "版本：0.10.5" });
        layout.AddChild(new Label { Text = "作者：惜梦yy" });
        layout.AddChild(new Label { Text = "快捷键说明：" });
        layout.AddChild(new Label { Text = "F7：打开或关闭当前新版界面" });
        layout.AddChild(new Label { Text = "有啥不知道的可以来B站看教程" });

        Button closeButton = new()
        {
            Text = "关闭",
            CustomMinimumSize = new Vector2(120.0f, 36.0f)
        };
        closeButton.Pressed += Hide;
        layout.AddChild(closeButton);
    }
}
