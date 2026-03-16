using Godot;
using InfectionCardReplacer.PackSystem.Models;
using System.Text.Json;

namespace InfectionCardReplacer.PackSystem.Services;

internal sealed class UserPackStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public List<UserPackState> LoadAll()
    {
        string path = GetStatePath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            List<UserPackState>? items = JsonSerializer.Deserialize<List<UserPackState>>(File.ReadAllText(path), JsonOptions);
            return items ?? [];
        }
        catch
        {
            return [];
        }
    }

    public UserPackState? GetByPackId(string packId)
    {
        return LoadAll().FirstOrDefault(item => item.PackId.Equals(packId, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsEnabledForSelection(string packId)
    {
        UserPackState? state = GetByPackId(packId);
        return state?.EnabledForSelection ?? true;
    }

    public void SetEnabledForSelection(string packId, bool enabled)
    {
        UserPackState state = GetByPackId(packId) ?? new UserPackState
        {
            PackId = packId
        };

        state.EnabledForSelection = enabled;
        Upsert(state);
    }

    public void Upsert(UserPackState state)
    {
        List<UserPackState> items = LoadAll();
        int index = items.FindIndex(item => item.PackId.Equals(state.PackId, StringComparison.OrdinalIgnoreCase));
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

    public void Remove(string packId)
    {
        List<UserPackState> items = LoadAll();
        items.RemoveAll(item => item.PackId.Equals(packId, StringComparison.OrdinalIgnoreCase));
        SaveAll(items);
    }

    private void SaveAll(List<UserPackState> items)
    {
        string path = GetStatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ProjectSettings.GlobalizePath("user://"));

        // 这里保留整份用户状态，是为了让删除与重装包时备注、收藏等偏好可以稳定继承。
        File.WriteAllText(path, JsonSerializer.Serialize(items, JsonOptions));
    }

    private static string GetStatePath()
    {
        return ProjectSettings.GlobalizePath("user://pack_state/user_pack_state.json");
    }
}
