using System.Reflection;
using System.IO;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace InfectionCardReplacer;

[HarmonyPatch]
internal static class InfectionPortraitPatch
{
    private const string BorderOverlayNodeName = "InfectionCustomBorderOverlay";
    private const string ReplacementTextureFileName = "infection_cover.png";
    private const string ReplacementBorderFileName = "infection_border.png";
    private static readonly Vector2 BorderScaleFactor = new(1.12f, 1.12f);
    private const float MinBorderPreviewWidth = 250.0f;

    private static readonly FieldInfo? PortraitField =
        typeof(NCard).GetField("_portrait", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo? AncientPortraitField =
        typeof(NCard).GetField("_ancientPortrait", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo? FrameField =
        typeof(NCard).GetField("_frame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo? PortraitBorderField =
        typeof(NCard).GetField("_portraitBorder", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo? BannerField =
        typeof(NCard).GetField("_banner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static Texture2D? _replacementTexture;
    private static Image? _replacementBorderSourceImage;
    private static bool _loggedVisualInfo;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
    private static void UpdateVisualsPostfix(NCard __instance, PileType pileType, CardPreviewMode previewMode)
    {
        TryReplacePortrait(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NCard), nameof(NCard._EnterTree))]
    private static void EnterTreePostfix(NCard __instance)
    {
        ResetCardVisuals(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NCard), nameof(NCard._ExitTree))]
    private static void ExitTreePrefix(NCard __instance)
    {
        ResetCardVisuals(__instance);
    }

    private static void TryReplacePortrait(NCard cardNode)
    {
        if (cardNode.Model is not CardModel model)
        {
            ResetCardVisuals(cardNode);
            return;
        }

        CardCatalogRegistry.Register(model);

        Texture2D? texture = null;
        if (!CardArtOverrideRegistry.TryGetTexture(model, out texture) && model is Infection)
        {
            texture = GetOrCreateReplacementTexture();
        }

        if (texture is null)
        {
            ResetCardVisuals(cardNode);
            return;
        }

        if (PortraitField?.GetValue(cardNode) is TextureRect portrait)
        {
            if (!ReferenceEquals(portrait.Texture, texture))
            {
                portrait.Texture = texture;
            }

            if (portrait.StretchMode != TextureRect.StretchModeEnum.KeepAspectCovered)
            {
                portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            }
        }

        if (AncientPortraitField?.GetValue(cardNode) is TextureRect ancientPortrait)
        {
            if (!ReferenceEquals(ancientPortrait.Texture, texture))
            {
                ancientPortrait.Texture = texture;
            }

            if (ancientPortrait.StretchMode != TextureRect.StretchModeEnum.KeepAspectCovered)
            {
                ancientPortrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            }
        }

        LogVisualInfoOnce(cardNode);
        ResetCardVisuals(cardNode);
    }

    private static Texture2D GetOrCreateReplacementTexture()
    {
        if (_replacementTexture is not null)
        {
            return _replacementTexture;
        }

        Texture2D? loadedTexture = LoadTextureFromModsFolder(ReplacementTextureFileName);
        if (loadedTexture is not null)
        {
            _replacementTexture = loadedTexture;
            return _replacementTexture;
        }

        const int width = 512;
        const int height = 512;

        Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

        for (int y = 0; y < height; y++)
        {
            float ny = y / (float)(height - 1);
            for (int x = 0; x < width; x++)
            {
                float nx = x / (float)(width - 1);
                float wave = Mathf.Sin((nx * 11.0f) + (ny * 17.0f)) * 0.5f + 0.5f;
                float glow = Mathf.Clamp(1.0f - (Mathf.Abs(nx - 0.5f) * 1.8f + Mathf.Abs(ny - 0.52f) * 1.4f), 0.0f, 1.0f);

                Color baseColor = new(
                    0.15f + 0.32f * wave + 0.28f * glow,
                    0.09f + 0.20f * wave + 0.16f * glow,
                    0.04f + 0.05f * wave,
                    1.0f);

                image.SetPixel(x, y, baseColor);
            }
        }

        DrawWorm(image, new Vector2(115, 126), new Vector2(360, 124), 28.0f, new Color(0.61f, 0.67f, 0.31f), 0.6f);
        DrawWorm(image, new Vector2(65, 266), new Vector2(390, 314), 32.0f, new Color(0.72f, 0.66f, 0.34f), -0.9f);
        DrawWorm(image, new Vector2(148, 405), new Vector2(431, 210), 24.0f, new Color(0.49f, 0.58f, 0.44f), 1.1f);
        DrawWorm(image, new Vector2(300, 92), new Vector2(225, 410), 22.0f, new Color(0.65f, 0.58f, 0.29f), 1.3f);

        DrawGlow(image, new Vector2(260, 250), 118.0f, new Color(0.93f, 0.12f, 0.03f, 0.38f));
        DrawGlow(image, new Vector2(160, 330), 96.0f, new Color(0.98f, 0.36f, 0.05f, 0.28f));

        _replacementTexture = ImageTexture.CreateFromImage(image);
        return _replacementTexture;
    }

    private static Image? GetOrCreateReplacementBorderSourceImage()
    {
        if (_replacementBorderSourceImage is not null)
        {
            return _replacementBorderSourceImage;
        }

        _replacementBorderSourceImage = LoadBorderImageFromModsFolder(ReplacementBorderFileName);
        return _replacementBorderSourceImage;
    }

    private static void LogVisualInfoOnce(NCard cardNode)
    {
        if (_loggedVisualInfo)
        {
            return;
        }

        _loggedVisualInfo = true;

        string frameInfo = DescribeTextureRect(FrameField?.GetValue(cardNode) as TextureRect, "_frame");
        string portraitBorderInfo = DescribeTextureRect(PortraitBorderField?.GetValue(cardNode) as TextureRect, "_portraitBorder");
        string bannerInfo = DescribeTextureRect(BannerField?.GetValue(cardNode) as TextureRect, "_banner");

        GD.Print($"[InfectionCardReplacer] Infection visual nodes: {frameInfo} | {portraitBorderInfo} | {bannerInfo}");
    }

    private static string DescribeTextureRect(TextureRect? rect, string name)
    {
        if (rect is null)
        {
            return $"{name}=null";
        }

        Texture2D? texture = rect.Texture;
        string resourcePath = texture?.ResourcePath;
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            resourcePath = "<no-resource-path>";
        }

        string textureSize = texture is null ? "null" : $"{texture.GetWidth()}x{texture.GetHeight()}";
        string rectSize = $"{rect.Size.X:0.##}x{rect.Size.Y:0.##}";

        return $"{name}[visible={rect.Visible}, rect={rectSize}, texture={textureSize}, path={resourcePath}]";
    }

    private static Texture2D? LoadTextureFromModsFolder(string fileName)
    {
        string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrWhiteSpace(assemblyDir))
        {
            return null;
        }

        string imagePath = Path.Combine(assemblyDir, fileName);
        if (!File.Exists(imagePath))
        {
            return null;
        }

        Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        Error error = image.Load(imagePath);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Failed to load InfectionCardReplacer texture: {imagePath} ({error})");
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static Image? LoadBorderImageFromModsFolder(string fileName)
    {
        string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrWhiteSpace(assemblyDir))
        {
            return null;
        }

        string imagePath = Path.Combine(assemblyDir, fileName);
        if (!File.Exists(imagePath))
        {
            return null;
        }

        Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        Error error = image.Load(imagePath);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Failed to load InfectionCardReplacer border texture: {imagePath} ({error})");
            return null;
        }

        RemoveCheckerboardBackground(image);
        return CropToVisibleBounds(image);
    }

    private static void RemoveCheckerboardBackground(Image image)
    {
        int width = image.GetWidth();
        int height = image.GetHeight();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = image.GetPixel(x, y);
                float max = Mathf.Max(pixel.R, Mathf.Max(pixel.G, pixel.B));
                float min = Mathf.Min(pixel.R, Mathf.Min(pixel.G, pixel.B));
                float saturation = max - min;
                float brightness = (pixel.R + pixel.G + pixel.B) / 3.0f;

                bool isLightGray = saturation < 0.06f && brightness > 0.72f;
                bool isNearWhite = brightness > 0.94f;

                if (isLightGray || isNearWhite)
                {
                    image.SetPixel(x, y, new Color(pixel.R, pixel.G, pixel.B, 0.0f));
                }
            }
        }
    }

    private static Image CropToVisibleBounds(Image image)
    {
        int width = image.GetWidth();
        int height = image.GetHeight();
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (image.GetPixel(x, y).A <= 0.01f)
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return image;
        }

        Rect2I cropRect = new(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return image.GetRegion(cropRect);
    }

    private static void ApplyFrameReplacement(NCard cardNode)
    {
        if (!GodotObject.IsInstanceValid(cardNode) || cardNode.Model is not Infection)
        {
            return;
        }

        Vector2 size = cardNode.GetCurrentSize();
        if (size.X <= 0.01f || size.Y <= 0.01f)
        {
            size = cardNode.Size;
        }

        if (size.X < MinBorderPreviewWidth)
        {
            if (cardNode.GetNodeOrNull<TextureRect>(BorderOverlayNodeName) is TextureRect hiddenOverlay)
            {
                hiddenOverlay.Visible = false;
            }

            return;
        }

        Texture2D? borderTexture = CreateBorderTextureForSize(size);
        if (borderTexture is null)
        {
            GD.PushWarning("[InfectionCardReplacer] infection_border.png could not be loaded from mods folder.");
            return;
        }

        if (FrameField?.GetValue(cardNode) is TextureRect frame)
        {
            frame.Visible = true;
            frame.Material = null;
            frame.SelfModulate = new Color(1.0f, 1.0f, 1.0f, 0.02f);
        }

        if (PortraitBorderField?.GetValue(cardNode) is TextureRect portraitBorder)
        {
            portraitBorder.Visible = false;
        }

        RemoveBorderOverlay(cardNode);
        TextureRect overlay = CreateBorderOverlay(cardNode);
        overlay.Texture = borderTexture;
        overlay.Visible = true;
        ApplyBorderLayout(cardNode, overlay);

        string overlayRect = $"{overlay.Position.X:0.##},{overlay.Position.Y:0.##},{overlay.Size.X:0.##}x{overlay.Size.Y:0.##}";
        GD.Print($"[InfectionCardReplacer] Applied custom Infection border overlay on NCard overlay_rect={overlayRect}");
    }

    private static TextureRect CreateBorderOverlay(NCard cardNode)
    {
        TextureRect overlay = new()
        {
            Name = BorderOverlayNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Material = null,
            SelfModulate = Colors.White,
            ZIndex = 100,
            TopLevel = true
        };

        cardNode.AddChild(overlay);
        return overlay;
    }

    private static void ApplyBorderLayout(NCard cardNode, TextureRect overlay)
    {
        Vector2 size = cardNode.GetCurrentSize();
        if (size.X <= 0.01f || size.Y <= 0.01f)
        {
            size = cardNode.Size;
        }

        Vector2 scaledSize = new(size.X * BorderScaleFactor.X, size.Y * BorderScaleFactor.Y);
        overlay.GlobalPosition = cardNode.GlobalPosition - scaledSize / 2.0f;
        overlay.Size = scaledSize;
        overlay.Scale = Vector2.One;
    }

    private static void ResetCardVisuals(NCard cardNode)
    {
        if (PortraitBorderField?.GetValue(cardNode) is TextureRect portraitBorder)
        {
            portraitBorder.Visible = true;
        }

        RemoveBorderOverlay(cardNode);
    }

    private static void RemoveBorderOverlay(NCard cardNode)
    {
        if (cardNode.GetNodeOrNull<TextureRect>(BorderOverlayNodeName) is TextureRect overlay &&
            GodotObject.IsInstanceValid(overlay))
        {
            overlay.QueueFree();
        }
    }

    private static Texture2D? CreateBorderTextureForSize(Vector2 targetSize)
    {
        Image? source = GetOrCreateReplacementBorderSourceImage();
        if (source is null)
        {
            return null;
        }

        int width = Mathf.Max(1, Mathf.RoundToInt(targetSize.X));
        int height = Mathf.Max(1, Mathf.RoundToInt(targetSize.Y));

        Image resized = source.Duplicate() as Image ?? source;
        resized.Resize(width, height, Image.Interpolation.Lanczos);
        return ImageTexture.CreateFromImage(resized);
    }

    private static void DrawWorm(Image image, Vector2 start, Vector2 end, float radius, Color color, float bend)
    {
        const int steps = 96;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 point = start.Lerp(end, t);
            float sway = Mathf.Sin(t * Mathf.Pi * 3.0f) * bend * 42.0f;
            point.X += sway;
            point.Y += Mathf.Cos(t * Mathf.Pi * 2.0f) * bend * 18.0f;

            float segmentRadius = radius * (0.78f + 0.22f * Mathf.Sin(t * Mathf.Pi));
            DrawCircle(image, point, segmentRadius, color);
            DrawCircle(image, point + new Vector2(-segmentRadius * 0.18f, -segmentRadius * 0.24f), segmentRadius * 0.32f, new Color(0.84f, 0.79f, 0.47f, 0.65f));
        }
    }

    private static void DrawGlow(Image image, Vector2 center, float radius, Color color)
    {
        int minX = Mathf.Max((int)(center.X - radius), 0);
        int maxX = Mathf.Min((int)(center.X + radius), image.GetWidth() - 1);
        int minY = Mathf.Max((int)(center.Y - radius), 0);
        int maxY = Mathf.Min((int)(center.Y + radius), image.GetHeight() - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float distance = center.DistanceTo(new Vector2(x, y));
                if (distance > radius)
                {
                    continue;
                }

                float alpha = (1.0f - distance / radius) * color.A;
                BlendPixel(image, x, y, new Color(color.R, color.G, color.B, alpha));
            }
        }
    }

    private static void DrawCircle(Image image, Vector2 center, float radius, Color color)
    {
        int minX = Mathf.Max((int)(center.X - radius), 0);
        int maxX = Mathf.Min((int)(center.X + radius), image.GetWidth() - 1);
        int minY = Mathf.Max((int)(center.Y - radius), 0);
        int maxY = Mathf.Min((int)(center.Y + radius), image.GetHeight() - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float distance = center.DistanceTo(new Vector2(x, y));
                if (distance > radius)
                {
                    continue;
                }

                float edge = Mathf.Clamp(1.0f - distance / radius, 0.0f, 1.0f);
                BlendPixel(image, x, y, new Color(color.R, color.G, color.B, 0.45f + edge * 0.55f));
            }
        }
    }

    private static void BlendPixel(Image image, int x, int y, Color top)
    {
        Color bottom = image.GetPixel(x, y);
        float outAlpha = top.A + bottom.A * (1.0f - top.A);
        if (outAlpha <= 0.0001f)
        {
            image.SetPixel(x, y, Colors.Transparent);
            return;
        }

        float r = (top.R * top.A + bottom.R * bottom.A * (1.0f - top.A)) / outAlpha;
        float g = (top.G * top.A + bottom.G * bottom.A * (1.0f - top.A)) / outAlpha;
        float b = (top.B * top.A + bottom.B * bottom.A * (1.0f - top.A)) / outAlpha;
        image.SetPixel(x, y, new Color(r, g, b, outAlpha));
    }
}
