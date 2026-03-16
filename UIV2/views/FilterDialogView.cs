using Godot;
using System;
using System.Collections.Generic;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class FilterDialogView : Window
{
    public event Action<IReadOnlyCollection<string>, bool>? FiltersChanged;

    private VBoxContainer? _listContainer;
    private CheckBox? _onlyReplacedCheckBox;
    private readonly Dictionary<string, CheckBox> _roleBoxes = new(StringComparer.OrdinalIgnoreCase);

    public override void _Ready()
    {
        Title = "角色筛选";
        InitialPosition = WindowInitialPosition.CenterMainWindowScreen;
        Size = new Vector2I(560, 620);
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
        layout.AddThemeConstantOverride("separation", 12);
        AddChild(layout);

        layout.AddChild(new Label { Text = "勾选要显示的角色后，主界面卡牌列表会立即刷新。" });

        _onlyReplacedCheckBox = new CheckBox
        {
            Text = "仅显示已替换",
            ButtonPressed = false
        };
        _onlyReplacedCheckBox.Toggled += _ => RaiseFiltersChanged();
        layout.AddChild(_onlyReplacedCheckBox);

        HBoxContainer actionRow = new();
        actionRow.AddThemeConstantOverride("separation", 8);
        layout.AddChild(actionRow);

        Button selectAllButton = new()
        {
            Text = "全选",
            CustomMinimumSize = new Vector2(120.0f, 36.0f)
        };
        selectAllButton.Pressed += SelectAll;
        actionRow.AddChild(selectAllButton);

        Button clearButton = new()
        {
            Text = "清空",
            CustomMinimumSize = new Vector2(120.0f, 36.0f)
        };
        clearButton.Pressed += ClearAll;
        actionRow.AddChild(clearButton);

        ScrollContainer scroll = new()
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        layout.AddChild(scroll);

        _listContainer = new VBoxContainer();
        _listContainer.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_listContainer);

        Button closeButton = new()
        {
            Text = "关闭",
            CustomMinimumSize = new Vector2(120.0f, 36.0f)
        };
        closeButton.Pressed += Hide;
        layout.AddChild(closeButton);
    }

    public void SetRoleItems(IReadOnlyList<SkinCenterRoleItem> roles, IReadOnlyCollection<string>? selectedRoles = null, bool onlyShowReplaced = false)
    {
        if (_listContainer is null)
        {
            return;
        }

        _roleBoxes.Clear();
        foreach (Node child in _listContainer.GetChildren())
        {
            _listContainer.RemoveChild(child);
            child.QueueFree();
        }

        if (_onlyReplacedCheckBox is not null)
        {
            _onlyReplacedCheckBox.ButtonPressed = onlyShowReplaced;
        }

        if (roles.Count == 0)
        {
            _listContainer.AddChild(new Label { Text = "当前还没有扫描到角色信息" });
            return;
        }

        foreach (SkinCenterRoleItem role in roles)
        {
            bool isSelected = selectedRoles is not null && selectedRoles.Count > 0
                ? selectedRoles.Contains(role.RoleKey)
                : true;

            HBoxContainer row = new()
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(460.0f, 32.0f)
            };
            row.AddThemeConstantOverride("separation", 10);

            CheckBox checkBox = new()
            {
                ButtonPressed = isSelected,
                CustomMinimumSize = new Vector2(28.0f, 28.0f)
            };
            checkBox.Toggled += _ => RaiseFiltersChanged();
            _roleBoxes[role.RoleKey] = checkBox;
            row.AddChild(checkBox);

            Label label = new()
            {
                Text = $"{role.DisplayName}  已替换 {role.ReplacementCount}/{role.CardCount}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                VerticalAlignment = VerticalAlignment.Center
            };
            label.AddThemeColorOverride("font_color", Colors.White);
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 1);
            row.AddChild(label);

            _listContainer.AddChild(row);
        }
    }

    private void SelectAll()
    {
        foreach (CheckBox checkBox in _roleBoxes.Values)
        {
            checkBox.ButtonPressed = true;
        }

        RaiseFiltersChanged();
    }

    private void ClearAll()
    {
        foreach (CheckBox checkBox in _roleBoxes.Values)
        {
            checkBox.ButtonPressed = false;
        }

        RaiseFiltersChanged();
    }

    private void RaiseFiltersChanged()
    {
        List<string> selectedRoles = [];
        foreach ((string role, CheckBox checkBox) in _roleBoxes)
        {
            if (checkBox.ButtonPressed)
            {
                selectedRoles.Add(role);
            }
        }

        FiltersChanged?.Invoke(selectedRoles, _onlyReplacedCheckBox?.ButtonPressed ?? false);
    }
}
