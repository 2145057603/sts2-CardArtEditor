using System.Reflection;
using System.Text.Json;

namespace InfectionCardReplacer;

internal static class CardArtHistoryRegistry
{
    internal sealed class HistoryEntry
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
    }

    private const int MaxEntries = 80;

    private static readonly List<HistoryEntry> Entries = [];
    private static bool _loaded;
    private static int _version;

    internal static int Version => _version;

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
            List<HistoryEntry>? entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json);
            if (entries is null)
            {
                return;
            }

            Entries.Clear();
            Entries.AddRange(entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Action) && !string.IsNullOrWhiteSpace(entry.Target))
                .Take(MaxEntries));
            _version++;
        }
        catch
        {
            Entries.Clear();
        }
    }

    internal static IReadOnlyList<HistoryEntry> GetEntries()
    {
        EnsureLoaded();
        return Entries.ToList();
    }

    internal static void RecordApply(string target, string imagePath)
    {
        AddEntry(new HistoryEntry
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Action = "应用替换",
            Target = target.Trim(),
            ImagePath = imagePath.Trim()
        });
    }

    internal static void RecordRemove(string target)
    {
        AddEntry(new HistoryEntry
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Action = "恢复默认",
            Target = target.Trim(),
            ImagePath = string.Empty
        });
    }

    internal static void Clear()
    {
        EnsureLoaded();
        Entries.Clear();
        _version++;
        Save();
    }

    private static void AddEntry(HistoryEntry entry)
    {
        EnsureLoaded();
        Entries.Insert(0, entry);
        if (Entries.Count > MaxEntries)
        {
            Entries.RemoveRange(MaxEntries, Entries.Count - MaxEntries);
        }

        _version++;
        Save();
    }

    private static void Save()
    {
        try
        {
            JsonSerializerOptions options = new()
            {
                WriteIndented = true
            };

            File.WriteAllText(GetConfigPath(), JsonSerializer.Serialize(Entries, options));
        }
        catch
        {
        }
    }

    private static string GetConfigPath()
    {
        string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return Path.Combine(assemblyDir ?? ".", "card_art_history.json");
    }
}
