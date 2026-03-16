using Godot;

namespace InfectionCardReplacer.UIV2;

internal sealed class SkinCenterCardItem
{
    public string CardId { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public bool HasReplacement { get; set; }
}

internal sealed class SkinCenterRoleItem
{
    public string RoleKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int CardCount { get; set; }
    public int ReplacementCount { get; set; }
}

internal sealed class SkinCenterPreviewData
{
    public string CardId { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string CardType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EnergyText { get; set; } = string.Empty;
    public string ReplacementPath { get; set; } = string.Empty;
    public Texture2D? OriginalTexture { get; set; }
    public Texture2D? ReplacementTexture { get; set; }
    public Texture2D? FrameTexture { get; set; }
    public Texture2D? BannerTexture { get; set; }
    public Texture2D? PortraitBorderTexture { get; set; }
    public Texture2D? EnergyIconTexture { get; set; }
    public Texture2D? HighlightTexture { get; set; }
    public Material? FrameMaterial { get; set; }
    public Material? BannerMaterial { get; set; }
    public Material? PortraitBorderMaterial { get; set; }
    public Material? HighlightMaterial { get; set; }
    public Color FrameModulate { get; set; } = Colors.White;
    public Color BannerModulate { get; set; } = Colors.White;
    public Color PortraitBorderModulate { get; set; } = Colors.White;
    public Color EnergyIconModulate { get; set; } = Colors.White;
    public Color HighlightModulate { get; set; } = Colors.White;
}
