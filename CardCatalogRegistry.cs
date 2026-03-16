using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using System.Collections;
using System.Reflection;

namespace InfectionCardReplacer;

internal static class CardCatalogRegistry
{
    internal sealed record Entry(string Title, string TypeName, string FullTypeName, string RoleName);

    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, CardModel> Models = new(StringComparer.OrdinalIgnoreCase);
    private static int _version;
    private static double _scanCooldown;
    private static long _nextModelDbAttemptTick;
    private static bool _modelDbLoaded;

    internal static int Version => _version;
    internal static int Count => Entries.Count;

    internal static void Register(CardModel? model)
    {
        if (model is null)
        {
            return;
        }

        string fullTypeName = model.GetType().FullName ?? model.GetType().Name;
        string roleName = ResolveRoleName(model);
        Entry entry = new(
            model.Title ?? string.Empty,
            model.GetType().Name,
            fullTypeName,
            roleName);

        if (Entries.TryGetValue(fullTypeName, out Entry? existing) && existing == entry)
        {
            return;
        }

        Entries[fullTypeName] = entry;
        Models[fullTypeName] = model;
        _version++;
    }

    internal static void EnsureLoaded()
    {
        if (_modelDbLoaded)
        {
            return;
        }

        long now = System.Environment.TickCount64;
        if (now < _nextModelDbAttemptTick)
        {
            return;
        }

        _nextModelDbAttemptTick = now + 1000;
        TryLoadFromModelDb();
    }

    internal static void TickScan(Node? root, double delta)
    {
        if (root is null)
        {
            return;
        }

        _scanCooldown -= delta;
        if (_scanCooldown > 0.0)
        {
            return;
        }

        _scanCooldown = 0.5;
        ScanNode(root);
    }

    internal static IReadOnlyList<Entry> GetEntries(string? filter = null, IReadOnlySet<string>? selectedRoles = null)
    {
        EnsureLoaded();

        IEnumerable<Entry> query = Entries.Values;
        if (selectedRoles is not null && selectedRoles.Count > 0)
        {
            query = query.Where(entry => selectedRoles.Contains(entry.RoleName));
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            string normalized = filter.Trim();
            query = query.Where(entry =>
                entry.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                entry.TypeName.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                entry.FullTypeName.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                entry.RoleName.Contains(normalized, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.FullTypeName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static IReadOnlyList<string> GetAllRoles()
    {
        EnsureLoaded();

        return Entries.Values
            .Select(entry => entry.RoleName)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string GetDisplayNameForTarget(string? target)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(target))
        {
            return string.Empty;
        }

        string normalizedTarget = target.Trim();
        Entry? entry = Entries.Values.FirstOrDefault(entry =>
            entry.FullTypeName.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
            entry.TypeName.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
            entry.Title.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase));

        if (entry is not null)
        {
            return entry.Title;
        }

        int lastDot = normalizedTarget.LastIndexOf('.');
        return lastDot >= 0 && lastDot < normalizedTarget.Length - 1
            ? normalizedTarget[(lastDot + 1)..]
            : normalizedTarget;
    }

    internal static CardModel? TryGetModel(string? target)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        string normalizedTarget = target.Trim();
        if (Models.TryGetValue(normalizedTarget, out CardModel? exactModel))
        {
            return exactModel;
        }

        foreach ((string _, CardModel model) in Models)
        {
            Type modelType = model.GetType();
            if (string.Equals(modelType.Name, normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(model.Title, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                return model;
            }
        }

        return null;
    }

    private static void ScanNode(Node node)
    {
        if (node is NCard cardNode && cardNode.Model is CardModel model)
        {
            Register(model);
        }

        foreach (Node child in node.GetChildren())
        {
            ScanNode(child);
        }
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
                return "无色";
            }
        }

        return "未知";
    }
    private static void TryLoadFromModelDb()
    {
        try
        {
            Type? modelDbType = Type.GetType("MegaCrit.Sts2.Core.Models.ModelDb, sts2", throwOnError: false);
            if (modelDbType is null)
            {
                return;
            }

            object? modelDb = ResolveModelDbInstance(modelDbType);
            if (modelDb is null)
            {
                return;
            }

            PropertyInfo? allCardsProperty = modelDbType.GetProperty("AllCards", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (allCardsProperty?.GetValue(modelDb) is not IEnumerable cards)
            {
                return;
            }

            int loadedCount = 0;
            foreach (object? card in cards)
            {
                if (card is CardModel model)
                {
                    Register(model);
                    loadedCount++;
                }
            }

            if (loadedCount > 0)
            {
                _modelDbLoaded = true;
                GD.Print($"[CardArtEditor] Preloaded {loadedCount} cards from ModelDb.");
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[CardArtEditor] Failed to preload cards from ModelDb: {ex.GetBaseException().Message}");
        }
    }

    private static object? ResolveModelDbInstance(Type modelDbType)
    {
        foreach (string methodName in new[] { "Singleton", "Get", "Instance" })
        {
            MethodInfo? method = modelDbType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (method is not null && !method.ContainsGenericParameters)
            {
                object? instance = method.Invoke(null, null);
                if (instance is not null)
                {
                    return instance;
                }
            }
        }

        foreach (string propertyName in new[] { "Instance", "Singleton", "Current" })
        {
            PropertyInfo? property = modelDbType.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is not null && property.GetIndexParameters().Length == 0)
            {
                object? instance = property.GetValue(null);
                if (instance is not null)
                {
                    return instance;
                }
            }
        }

        foreach (string fieldName in new[] { "Instance", "Singleton", "_instance" })
        {
            FieldInfo? field = modelDbType.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is not null)
            {
                object? instance = field.GetValue(null);
                if (instance is not null)
                {
                    return instance;
                }
            }
        }

        return null;
    }
}

