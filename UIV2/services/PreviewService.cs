using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace InfectionCardReplacer.UIV2.Services;

internal sealed class PreviewService
{
    private sealed class LiveCardVisualSample
    {
        public string SourceCardId { get; init; } = string.Empty;
        public string SourceTitle { get; init; } = string.Empty;
        public string SourceRole { get; init; } = string.Empty;
        public string VisualSummary { get; init; } = string.Empty;
        public Texture2D? FrameTexture { get; init; }
        public Texture2D? BannerTexture { get; init; }
        public Texture2D? PortraitBorderTexture { get; init; }
        public Texture2D? EnergyIconTexture { get; init; }
        public Texture2D? HighlightTexture { get; init; }
        public Material? FrameMaterial { get; init; }
        public Material? BannerMaterial { get; init; }
        public Material? PortraitBorderMaterial { get; init; }
        public Material? HighlightMaterial { get; init; }
        public Color FrameModulate { get; init; } = Colors.White;
        public Color BannerModulate { get; init; } = Colors.White;
        public Color PortraitBorderModulate { get; init; } = Colors.White;
        public Color EnergyIconModulate { get; init; } = Colors.White;
        public Color HighlightModulate { get; init; } = Colors.White;
    }

    private static readonly BindingFlags InstanceBindings =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly FieldInfo? PortraitField = typeof(NCard).GetField("_portrait", InstanceBindings);
    private static readonly FieldInfo? FrameField = typeof(NCard).GetField("_frame", InstanceBindings);
    private static readonly FieldInfo? BannerField = typeof(NCard).GetField("_banner", InstanceBindings);
    private static readonly FieldInfo? PortraitBorderField = typeof(NCard).GetField("_portraitBorder", InstanceBindings);
    private static readonly FieldInfo? EnergyIconField = typeof(NCard).GetField("_energyIcon", InstanceBindings);
    private static readonly Dictionary<string, LiveCardVisualSample> CardVisualCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, LiveCardVisualSample> RoleVisualCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> LastLoggedPreviewState = new(StringComparer.OrdinalIgnoreCase);

    public SkinCenterPreviewData BuildPreviewData(string cardId, string? replacementPathOverride = null)
    {
        CardModel? model = CardCatalogRegistry.TryGetModel(cardId);
        SkinCenterPreviewData data = new()
        {
            CardId = cardId
        };

        if (model is null)
        {
            return data;
        }

        data.CardName = model.Title ?? string.Empty;
        data.CardType = FormatCardType(model);
        data.Description = BuildPreviewDescription(model);
        data.EnergyText = BuildEnergyText(model);
        data.OriginalTexture = model.Portrait;
        data.FrameTexture = model.Frame;
        data.BannerTexture = model.BannerTexture;
        data.PortraitBorderTexture = model.PortraitBorder;
        data.EnergyIconTexture = model.EnergyIcon;
        TryApplyLiveCardVisuals(cardId, model, data);
        data.ReplacementPath = string.IsNullOrWhiteSpace(replacementPathOverride)
            ? CardArtOverrideRegistry.GetOverrideImagePath(cardId)
            : replacementPathOverride;

        if (!string.IsNullOrWhiteSpace(replacementPathOverride))
        {
            data.ReplacementTexture = LoadTexture(replacementPathOverride);
        }
        else if (CardArtOverrideRegistry.TryGetTextureForTarget(cardId, out Texture2D? texture))
        {
            data.ReplacementTexture = texture;
        }

        return data;
    }

    private static void TryApplyLiveCardVisuals(string cardId, CardModel model, SkinCenterPreviewData data)
    {
        if (Engine.GetMainLoop() is SceneTree tree && tree.Root is not null)
        {
            RefreshVisualCaches(tree.Root);
        }

        string targetCardId = string.IsNullOrWhiteSpace(cardId)
            ? model.GetType().FullName ?? model.GetType().Name
            : cardId;
        string targetRole = ResolveRoleName(model);
        string targetTitle = model.Title ?? string.Empty;

        string matchSource = "model";
        LiveCardVisualSample? sample = null;

        if (CardVisualCache.TryGetValue(targetCardId, out LiveCardVisualSample? exactSample))
        {
            sample = exactSample;
            matchSource = "exact-cache";
        }
        else if (!string.IsNullOrWhiteSpace(targetRole) &&
                 !string.Equals(targetRole, "unknown", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(targetRole, "未知", StringComparison.OrdinalIgnoreCase) &&
                 RoleVisualCache.TryGetValue(targetRole, out LiveCardVisualSample? roleSample))
        {
            sample = roleSample;
            matchSource = "role-cache";
        }

        if (sample is not null)
        {
            ApplyVisualSample(sample, data);
        }

        LogPreviewDecision(targetCardId, model, data, matchSource, sample, targetTitle, targetRole);
    }

    private static void RefreshVisualCaches(Node root)
    {
        foreach (NCard cardNode in EnumerateCards(root))
        {
            if (cardNode.Model is not CardModel liveModel)
            {
                continue;
            }

            LiveCardVisualSample? sample = CreateVisualSample(cardNode, liveModel);
            if (sample is null)
            {
                continue;
            }

            CardVisualCache[sample.SourceCardId] = sample;

            if (!string.IsNullOrWhiteSpace(sample.SourceRole) &&
                !string.Equals(sample.SourceRole, "unknown", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(sample.SourceRole, "未知", StringComparison.OrdinalIgnoreCase))
            {
                RoleVisualCache[sample.SourceRole] = sample;
            }
        }
    }

    private static IEnumerable<NCard> EnumerateCards(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is NCard card)
            {
                yield return card;
            }

            foreach (NCard nestedCard in EnumerateCards(child))
            {
                yield return nestedCard;
            }
        }
    }

    private static LiveCardVisualSample? CreateVisualSample(NCard cardNode, CardModel liveModel)
    {
        TextureRect? portraitRect = PortraitField?.GetValue(cardNode) as TextureRect;
        TextureRect? frameRect = FrameField?.GetValue(cardNode) as TextureRect;
        TextureRect? bannerRect = BannerField?.GetValue(cardNode) as TextureRect;
        TextureRect? borderRect = PortraitBorderField?.GetValue(cardNode) as TextureRect;
        TextureRect? energyIconRect = EnergyIconField?.GetValue(cardNode) as TextureRect;
        TextureRect? highlightRect = FindTextureRectField(cardNode, "CardHighlight");

        if (portraitRect?.Texture is null &&
            frameRect?.Texture is null &&
            bannerRect?.Texture is null &&
            borderRect?.Texture is null &&
            energyIconRect?.Texture is null &&
            highlightRect?.Texture is null)
        {
            return null;
        }

        return new LiveCardVisualSample
        {
            SourceCardId = liveModel.GetType().FullName ?? liveModel.GetType().Name,
            SourceTitle = liveModel.Title ?? string.Empty,
            SourceRole = ResolveRoleName(liveModel),
            VisualSummary = DescribeCardTextureRects(cardNode),
            FrameTexture = frameRect?.Texture,
            BannerTexture = bannerRect?.Texture,
            PortraitBorderTexture = borderRect?.Texture,
            EnergyIconTexture = energyIconRect?.Texture,
            HighlightTexture = highlightRect?.Texture,
            FrameMaterial = frameRect?.Material,
            BannerMaterial = bannerRect?.Material,
            PortraitBorderMaterial = borderRect?.Material,
            HighlightMaterial = highlightRect?.Material,
            FrameModulate = CombineVisualColor(frameRect),
            BannerModulate = CombineVisualColor(bannerRect),
            PortraitBorderModulate = CombineVisualColor(borderRect),
            EnergyIconModulate = CombineVisualColor(energyIconRect),
            HighlightModulate = CombineVisualColor(highlightRect)
        };
    }

    private static void ApplyVisualSample(LiveCardVisualSample sample, SkinCenterPreviewData data)
    {
        data.FrameTexture = sample.FrameTexture ?? data.FrameTexture;
        data.BannerTexture = sample.BannerTexture ?? data.BannerTexture;
        data.PortraitBorderTexture = sample.PortraitBorderTexture ?? data.PortraitBorderTexture;
        data.EnergyIconTexture = sample.EnergyIconTexture ?? data.EnergyIconTexture;
        data.HighlightTexture = sample.HighlightTexture ?? data.HighlightTexture;
        data.FrameMaterial = sample.FrameMaterial;
        data.BannerMaterial = sample.BannerMaterial;
        data.PortraitBorderMaterial = sample.PortraitBorderMaterial;
        data.HighlightMaterial = sample.HighlightMaterial;
        data.FrameModulate = sample.FrameModulate;
        data.BannerModulate = sample.BannerModulate;
        data.PortraitBorderModulate = sample.PortraitBorderModulate;
        data.EnergyIconModulate = sample.EnergyIconModulate;
        data.HighlightModulate = sample.HighlightModulate;
    }

    private static void LogPreviewDecision(
        string cardId,
        CardModel model,
        SkinCenterPreviewData data,
        string matchSource,
        LiveCardVisualSample? sample,
        string targetTitle,
        string targetRole)
    {
        string signature =
            $"{matchSource}|{sample?.SourceCardId}|{DescribeTexture(data.FrameTexture)}|{DescribeTexture(data.BannerTexture)}|" +
            $"{DescribeTexture(data.PortraitBorderTexture)}|{DescribeTexture(data.EnergyIconTexture)}|{DescribeTexture(data.HighlightTexture)}|" +
            $"{DescribeColor(data.FrameModulate)}|{DescribeColor(data.BannerModulate)}|" +
            $"{DescribeColor(data.PortraitBorderModulate)}|{DescribeColor(data.EnergyIconModulate)}|{DescribeColor(data.HighlightModulate)}|" +
            $"{DescribeMaterial(data.FrameMaterial)}|{DescribeMaterial(data.BannerMaterial)}|{DescribeMaterial(data.PortraitBorderMaterial)}|{DescribeMaterial(data.HighlightMaterial)}";

        if (LastLoggedPreviewState.TryGetValue(cardId, out string? previousSignature) &&
            string.Equals(previousSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        LastLoggedPreviewState[cardId] = signature;

        string logLine =
            $"[CardArtEditor][UIV2][Preview] cardId={cardId} title={targetTitle} role={targetRole} " +
            $"type={model.GetType().Name} match={matchSource} sourceCard={sample?.SourceCardId ?? "-"} " +
            $"sourceTitle={sample?.SourceTitle ?? "-"} sourceRole={sample?.SourceRole ?? "-"} " +
            $"frame={DescribeTexture(data.FrameTexture)} frameMod={DescribeColor(data.FrameModulate)} " +
            $"banner={DescribeTexture(data.BannerTexture)} bannerMod={DescribeColor(data.BannerModulate)} " +
            $"border={DescribeTexture(data.PortraitBorderTexture)} borderMod={DescribeColor(data.PortraitBorderModulate)} " +
            $"energy={DescribeTexture(data.EnergyIconTexture)} energyMod={DescribeColor(data.EnergyIconModulate)} " +
            $"highlight={DescribeTexture(data.HighlightTexture)} highlightMod={DescribeColor(data.HighlightModulate)} " +
            $"frameMat={DescribeMaterial(data.FrameMaterial)} bannerMat={DescribeMaterial(data.BannerMaterial)} " +
            $"borderMat={DescribeMaterial(data.PortraitBorderMaterial)} highlightMat={DescribeMaterial(data.HighlightMaterial)} " +
            $"layers={sample?.VisualSummary ?? "-"}";

        // 预览诊断保留控制台输出，不再自动生成本地日志文件。
        GD.Print(logLine);
    }

    private static string DescribeTexture(Texture2D? texture)
    {
        if (texture is null)
        {
            return "null";
        }

        string resourcePath = texture.ResourcePath;
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            resourcePath = "<no-resource-path>";
        }

        return $"{texture.GetWidth()}x{texture.GetHeight()}@{resourcePath}";
    }

    private static string DescribeColor(Color color)
    {
        return $"{color.R:0.###},{color.G:0.###},{color.B:0.###},{color.A:0.###}";
    }

    private static string DescribeMaterial(Material? material)
    {
        if (material is null)
        {
            return "null";
        }

        return string.IsNullOrWhiteSpace(material.ResourcePath)
            ? material.GetType().Name
            : material.ResourcePath;
    }

    private static Color CombineVisualColor(TextureRect? rect)
    {
        if (rect is null)
        {
            return Colors.White;
        }

        Color a = rect.Modulate;
        Color b = rect.SelfModulate;
        return new Color(a.R * b.R, a.G * b.G, a.B * b.B, a.A * b.A);
    }

    private static string DescribeCardTextureRects(NCard cardNode)
    {
        List<string> segments = [];
        FieldInfo[] fields = cardNode.GetType().GetFields(InstanceBindings);
        foreach (FieldInfo field in fields)
        {
            if (!typeof(TextureRect).IsAssignableFrom(field.FieldType))
            {
                continue;
            }

            TextureRect? rect = field.GetValue(cardNode) as TextureRect;
            if (rect is null)
            {
                continue;
            }

            segments.Add(DescribeTextureRectField(field.Name, rect));
        }

        return string.Join(";", segments);
    }

    private static string DescribeTextureRectField(string fieldName, TextureRect rect)
    {
        string materialName = rect.Material is null
            ? "null"
            : string.IsNullOrWhiteSpace(rect.Material.ResourcePath)
                ? rect.Material.GetType().Name
                : rect.Material.ResourcePath;

        return
            $"{fieldName}[tex={DescribeTexture(rect.Texture)},mod={DescribeColor(rect.Modulate)},self={DescribeColor(rect.SelfModulate)}," +
            $"visible={rect.Visible},material={materialName}]";
    }

    private static TextureRect? FindTextureRectField(NCard cardNode, string partialName)
    {
        foreach (FieldInfo field in cardNode.GetType().GetFields(InstanceBindings))
        {
            if (!typeof(TextureRect).IsAssignableFrom(field.FieldType))
            {
                continue;
            }

            if (field.Name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            return field.GetValue(cardNode) as TextureRect;
        }

        return null;
    }

    private static Texture2D? LoadTexture(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !System.IO.File.Exists(imagePath))
        {
            return null;
        }

        Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        if (image.Load(imagePath) != Error.Ok)
        {
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static string FormatCardType(CardModel model)
    {
        return model.Type switch
        {
            MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack => "攻击",
            MegaCrit.Sts2.Core.Entities.Cards.CardType.Skill => "技能",
            MegaCrit.Sts2.Core.Entities.Cards.CardType.Power => "能力",
            MegaCrit.Sts2.Core.Entities.Cards.CardType.Status => "状态",
            MegaCrit.Sts2.Core.Entities.Cards.CardType.Curse => "诅咒",
            MegaCrit.Sts2.Core.Entities.Cards.CardType.Quest => "任务",
            _ => model.Type.ToString()
        };
    }

    private static string BuildPreviewDescription(CardModel model)
    {
        string text = model.Description?.GetFormattedText() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = Regex.Replace(text, @"\{[^{}]*\}", string.Empty);
        text = text.Replace("[gold]", string.Empty, StringComparison.OrdinalIgnoreCase);
        text = text.Replace("[/gold]", string.Empty, StringComparison.OrdinalIgnoreCase);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private static string BuildEnergyText(CardModel model)
    {
        if (model.EnergyIcon is null)
        {
            return string.Empty;
        }

        foreach (string propertyName in new[] { "BaseCost", "Cost", "CostForTurn", "CurrentCost", "CardCost" })
        {
            PropertyInfo? property = model.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(model) is int intValue)
            {
                return intValue < 0 ? "X" : intValue.ToString();
            }
        }

        foreach (string fieldName in new[] { "BaseCost", "Cost", "CostForTurn", "CurrentCost", "_cost" })
        {
            FieldInfo? field = model.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(model) is int intValue)
            {
                return intValue < 0 ? "X" : intValue.ToString();
            }
        }

        return "?";
    }

    private static string ResolveRoleName(CardModel model)
    {
        if (model.Pool is not null)
        {
            string poolTitle = model.Pool.Title?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(poolTitle))
            {
                return poolTitle;
            }

            if (model.Pool.IsColorless)
            {
                return "colorless";
            }
        }

        return "unknown";
    }
}
