using Godot;
using InfectionCardReplacer.PackSystem.Models;
using System.Text.Json;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class PackEntryUserStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public List<PackEntryUserState> LoadAll()
    {
        string path = GetStatePath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            List<PackEntryUserState>? items = JsonSerializer.Deserialize<List<PackEntryUserState>>(File.ReadAllText(path), JsonOptions);
            return items ?? [];
        }
        catch
        {
            return [];
        }
    }

    public PackEntryUserState? Get(string packId, string relativePath)
    {
        string key = BuildEntryKey(packId, relativePath);
        return LoadAll().FirstOrDefault(item => item.EntryKey.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public void Upsert(string packId, string relativePath, string userNote)
    {
        string key = BuildEntryKey(packId, relativePath);
        List<PackEntryUserState> items = LoadAll();
        int index = items.FindIndex(item => item.EntryKey.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(userNote))
        {
            if (index >= 0)
            {
                items.RemoveAt(index);
                SaveAll(items);
            }
            return;
        }

        PackEntryUserState state = new()
        {
            EntryKey = key,
            PackId = packId,
            RelativePath = relativePath,
            UserNote = userNote.Trim()
        };

        if (index >= 0)
        {
            items[index] = state;
        }
        else
        {
            items.Add(state);
        }

        SaveAll(items);
    }

    public static string BuildEntryKey(string packId, string relativePath)
    {
        string normalized = relativePath.Replace("\\", "/", StringComparison.Ordinal);
        return $"{packId}|{normalized}".ToLowerInvariant();
    }

    private void SaveAll(List<PackEntryUserState> items)
    {
        string path = GetStatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ProjectSettings.GlobalizePath("user://"));

        // 图片备注独立存储，避免和卡包整体备注互相覆盖，也方便未来做按图搜索。
        File.WriteAllText(path, JsonSerializer.Serialize(items, JsonOptions));
    }

    private static string GetStatePath()
    {
        return ProjectSettings.GlobalizePath("user://pack_state/pack_entry_user_state.json");
    }
}
