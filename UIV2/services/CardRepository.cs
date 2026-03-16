using System;
using System.Collections.Generic;
using System.Linq;

namespace InfectionCardReplacer.UIV2.Services;

internal sealed class CardRepository
{
    public List<SkinCenterCardItem> GetAllCards(IReadOnlySet<string>? selectedRoles = null, bool onlyShowReplaced = false)
    {
        return MapEntries(CardCatalogRegistry.GetEntries(selectedRoles: selectedRoles), onlyShowReplaced);
    }

    public List<SkinCenterCardItem> SearchCards(string keyword, IReadOnlySet<string>? selectedRoles = null, bool onlyShowReplaced = false)
    {
        IEnumerable<CardCatalogRegistry.Entry> entries = string.IsNullOrWhiteSpace(keyword)
            ? CardCatalogRegistry.GetEntries(selectedRoles: selectedRoles)
            : CardCatalogRegistry.GetEntries(keyword, selectedRoles);

        return MapEntries(entries, onlyShowReplaced);
    }

    public SkinCenterCardItem? GetCard(string cardId)
    {
        return GetAllCards().FirstOrDefault(card => card.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
    }

    public List<string> GetAllRoles()
    {
        return CardCatalogRegistry.GetAllRoles().ToList();
    }

    public List<SkinCenterRoleItem> GetRoleSummaries()
    {
        return CardCatalogRegistry.GetAllRoles()
            .Select(role =>
            {
                IReadOnlyList<CardCatalogRegistry.Entry> entries = CardCatalogRegistry.GetEntries(selectedRoles: new HashSet<string>([role], StringComparer.OrdinalIgnoreCase));
                int replacementCount = entries.Count(entry => CardArtOverrideRegistry.TryGetTextureForTarget(entry.FullTypeName, out _));
                return new SkinCenterRoleItem
                {
                    RoleKey = role,
                    DisplayName = FormatRoleDisplayName(role),
                    CardCount = entries.Count,
                    ReplacementCount = replacementCount
                };
            })
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<SkinCenterCardItem> MapEntries(IEnumerable<CardCatalogRegistry.Entry> entries, bool onlyShowReplaced)
    {
        IEnumerable<SkinCenterCardItem> mapped = entries.Select(entry => new SkinCenterCardItem
        {
            CardId = entry.FullTypeName,
            CardName = entry.Title,
            Subtitle = entry.TypeName,
            HasReplacement = CardArtOverrideRegistry.TryGetTextureForTarget(entry.FullTypeName, out _)
        });

        if (onlyShowReplaced)
        {
            mapped = mapped.Where(item => item.HasReplacement);
        }

        return mapped.ToList();
    }

    private static string FormatRoleDisplayName(string role)
    {
        string normalizedRole = role.Trim();
        (string englishName, string chineseName) = normalizedRole.ToLowerInvariant() switch
        {
            "colorless" or "无色" => ("colorless", "无色"),
            "curse" or "诅咒" => ("curse", "诅咒"),
            "defect" or "故障机器人" => ("defect", "故障机器人"),
            "event" or "事件" => ("event", "事件"),
            "ironclad" or "铁甲战士" => ("ironclad", "铁甲战士"),
            "necrobinder" or "死灵绑定者" => ("necrobinder", "死灵绑定者"),
            "regent" or "摄政者" => ("regent", "摄政者"),
            "silent" or "静默猎手" => ("silent", "静默猎手"),
            "status" or "状态" => ("status", "状态"),
            "token" or "衍生牌" => ("token", "衍生牌"),
            "unknown" or "未知" => ("unknown", "未知"),
            _ => (normalizedRole, string.Empty)
        };

        if (string.IsNullOrWhiteSpace(chineseName))
        {
            return englishName;
        }

        return $"{englishName} ({chineseName})";
    }
}
