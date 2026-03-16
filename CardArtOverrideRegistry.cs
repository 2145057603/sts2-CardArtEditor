using System.Reflection;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace InfectionCardReplacer;

internal static class CardArtOverrideRegistry
{
    internal sealed class OverrideExportEntry
    {
        public string Target { get; init; } = string.Empty;
        public string ImagePath { get; init; } = string.Empty;
    }

    private sealed class OverrideConfig
    {
        public List<OverrideEntry> Overrides { get; set; } = [];
    }

    private sealed class OverrideEntry
    {
        public string Target { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
    }

    private static readonly Dictionary<string, OverrideEntry> Overrides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string?> ResolvedImagePathCache = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    internal static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        string configPath = GetConfigPath();
        if (!File.Exists(configPath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(configPath);
            OverrideConfig? config = JsonSerializer.Deserialize<OverrideConfig>(json);
            if (config?.Overrides is null)
            {
                return;
            }

            foreach (OverrideEntry entry in config.Overrides)
            {
                if (string.IsNullOrWhiteSpace(entry.Target) || string.IsNullOrWhiteSpace(entry.ImagePath))
                {
                    continue;
                }

                Overrides[Normalize(entry.Target)] = entry;
            }

            ResolvedImagePathCache.Clear();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[InfectionCardReplacer] Failed to load card art overrides: {ex.Message}");
        }
    }

    internal static bool TryGetTexture(CardModel model, out Texture2D? texture)
    {
        EnsureLoaded();

        string modelKey = model.GetType().FullName ?? model.GetType().Name;
        if (ResolvedImagePathCache.TryGetValue(modelKey, out string? cachedImagePath))
        {
            texture = string.IsNullOrWhiteSpace(cachedImagePath) ? null : LoadTexture(cachedImagePath);
            return texture is not null;
        }

        foreach (OverrideEntry entry in Overrides.Values)
        {
            if (!Matches(model, entry.Target))
            {
                continue;
            }

            ResolvedImagePathCache[modelKey] = entry.ImagePath;
            texture = LoadTexture(entry.ImagePath);
            return texture is not null;
        }

        ResolvedImagePathCache[modelKey] = null;
        texture = null;
        return false;
    }

    internal static bool TryGetTextureForTarget(string target, out Texture2D? texture)
    {
        EnsureLoaded();

        string normalizedTarget = Normalize(target);
        if (Overrides.TryGetValue(normalizedTarget, out OverrideEntry? exactEntry))
        {
            texture = LoadTexture(exactEntry.ImagePath);
            return texture is not null;
        }

        foreach (OverrideEntry entry in Overrides.Values)
        {
            if (!Normalize(entry.Target).Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            texture = LoadTexture(entry.ImagePath);
            return texture is not null;
        }

        texture = null;
        return false;
    }

    internal static string SetOverride(string target, string imagePath)
    {
        EnsureLoaded();

        string normalizedTarget = Normalize(target);
        OverrideEntry entry = new()
        {
            Target = target.Trim(),
            ImagePath = imagePath.Trim()
        };

        Overrides[normalizedTarget] = entry;
        TextureCache.Remove(entry.ImagePath);
        ResolvedImagePathCache.Clear();
        Save();
        CardArtHistoryRegistry.RecordApply(entry.Target, entry.ImagePath);
        return $"Applied override for '{entry.Target}'.";
    }

    internal static string GetOverrideImagePath(string? target)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(target))
        {
            return string.Empty;
        }

        string normalizedTarget = Normalize(target);
        if (Overrides.TryGetValue(normalizedTarget, out OverrideEntry? exactEntry))
        {
            return exactEntry.ImagePath;
        }

        foreach (OverrideEntry entry in Overrides.Values)
        {
            if (Normalize(entry.Target).Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                return entry.ImagePath;
            }
        }

        return string.Empty;
    }

    internal static string RemoveOverride(string target)
    {
        EnsureLoaded();

        string normalizedTarget = Normalize(target);
        if (!Overrides.Remove(normalizedTarget))
        {
            return $"No override found for '{target.Trim()}'.";
        }

        ResolvedImagePathCache.Clear();
        Save();
        CardArtHistoryRegistry.RecordRemove(target);
        return $"Removed override for '{target.Trim()}'.";
    }

    internal static string RemoveAllOverrides()
    {
        EnsureLoaded();

        int removedCount = Overrides.Count;
        if (removedCount == 0)
        {
            return "当前没有已替换的卡牌。";
        }

        // 这里统一清空全部覆盖项，并把运行时缓存一并失效，保证主界面马上回到默认卡图。
        Overrides.Clear();
        TextureCache.Clear();
        ResolvedImagePathCache.Clear();
        Save();
        return $"已恢复全部默认，共清除 {removedCount} 项替换。";
    }

    internal static IReadOnlyList<OverrideExportEntry> GetOverrideExportEntries()
    {
        EnsureLoaded();

        return Overrides.Values
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Target) && !string.IsNullOrWhiteSpace(entry.ImagePath))
            .Select(entry => new OverrideExportEntry
            {
                Target = entry.Target,
                ImagePath = entry.ImagePath
            })
            .OrderBy(entry => entry.Target, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string DescribeCard(CardModel? model)
    {
        if (model is null)
        {
            return "Current card: <none>";
        }

        return $"Current card: {model.Title} | {model.GetType().FullName}";
    }

    internal static string GetPreferredTarget(CardModel? model)
    {
        if (model is null)
        {
            return string.Empty;
        }

        return model.GetType().FullName ?? model.Title ?? string.Empty;
    }

    internal static IReadOnlyList<string> GetHelpLines()
    {
        return
        [
            "Keyword: artcenter / cardart / 卡面",
            "Target: card title, type name, or full type name",
            "Image path: any local png/jpg file path"
        ];
    }

    private static bool Matches(CardModel model, string target)
    {
        string normalizedTarget = Normalize(target);
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return false;
        }

        string title = Normalize(model.Title);
        string typeName = Normalize(model.GetType().Name);
        string fullTypeName = Normalize(model.GetType().FullName);

        return title.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
               fullTypeName.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
               fullTypeName.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static Texture2D? LoadTexture(string imagePath)
    {
        if (TextureCache.TryGetValue(imagePath, out Texture2D? cachedTexture))
        {
            return cachedTexture;
        }

        if (!File.Exists(imagePath))
        {
            return null;
        }

        Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        Error error = image.Load(imagePath);
        if (error != Error.Ok)
        {
            GD.PushWarning($"[InfectionCardReplacer] Failed to load override image '{imagePath}': {error}");
            return null;
        }

        Texture2D texture = ImageTexture.CreateFromImage(image);
        TextureCache[imagePath] = texture;
        return texture;
    }

    private static void Save()
    {
        try
        {
            OverrideConfig config = new()
            {
                Overrides = Overrides.Values
                    .OrderBy(entry => entry.Target, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            JsonSerializerOptions options = new()
            {
                WriteIndented = true
            };

            File.WriteAllText(GetConfigPath(), JsonSerializer.Serialize(config, options));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[InfectionCardReplacer] Failed to save card art overrides: {ex.Message}");
        }
    }

    private static string GetConfigPath()
    {
        string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return Path.Combine(assemblyDir ?? ".", "card_art_overrides.json");
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
