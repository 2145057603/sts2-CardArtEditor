using Godot;

namespace InfectionCardReplacer.UIV2.Views;

internal sealed partial class ResourceListItemView : PanelContainer
{
    public void Bind(SkinCenterCardItem item)
    {
        Label label = GetNodeOrNull<Label>("NameLabel") ?? new Label { Name = "NameLabel" };
        if (label.GetParent() is null)
        {
            AddChild(label);
        }

        label.Text = string.IsNullOrWhiteSpace(item.Subtitle)
            ? item.CardName
            : $"{item.CardName} [{item.Subtitle}]";
    }
}
