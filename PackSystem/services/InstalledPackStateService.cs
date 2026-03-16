using Godot;
using InfectionCardReplacer.PackSystem.Models;
using System.Text.Json;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class InstalledPackStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public List<InstalledPackState> LoadAll()
    {
        string path = GetStatePath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            List<InstalledPackState>? items = JsonSerializer.Deserialize<List<InstalledPackState>>(File.ReadAllText(path), JsonOptions);
            return items ?? [];
        }
        catch
        {
            return [];
        }
    }

    public InstalledPackState? GetByInstance(string packId, PackSourceType sourceType, string installRoot)
    {
        string instanceId = BuildInstanceId(packId, sourceType, installRoot);
        return LoadAll().FirstOrDefault(item => item.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
    }

    public void Upsert(InstalledPackState state)
    {
        if (string.IsNullOrWhiteSpace(state.InstanceId))
        {
            state.InstanceId = BuildInstanceId(state.PackId, state.SourceType, state.InstallRoot);
        }

        List<InstalledPackState> items = LoadAll();
        int index = items.FindIndex(item => item.InstanceId.Equals(state.InstanceId, StringComparison.OrdinalIgnoreCase));
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

    public void Remove(string packId, PackSourceType sourceType, string installRoot)
    {
        string instanceId = BuildInstanceId(packId, sourceType, installRoot);
        List<InstalledPackState> items = LoadAll();
        items.RemoveAll(item => item.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
        SaveAll(items);
    }

    public static string BuildInstanceId(string packId, PackSourceType sourceType, string installRoot)
    {
        return $"{packId}|{sourceType}|{installRoot}".ToLowerInvariant();
    }

    private void SaveAll(List<InstalledPackState> items)
    {
        string path = GetStatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ProjectSettings.GlobalizePath("user://"));

        // 安装实例状态主要追踪运行时扫描、缓存与损坏情况，不与逻辑包的用户备注混在一起。
        File.WriteAllText(path, JsonSerializer.Serialize(items, JsonOptions));
    }

    private static string GetStatePath()
    {
        return ProjectSettings.GlobalizePath("user://pack_state/installed_pack_state.json");
    }
}
