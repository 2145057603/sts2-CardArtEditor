using System.Collections.Generic;
using System.Linq;

namespace InfectionCardReplacer.UIV2.Services;

internal sealed class HistoryService
{
    public List<CardArtHistoryRegistry.HistoryEntry> GetEntries()
    {
        return CardArtHistoryRegistry.GetEntries().ToList();
    }
}
