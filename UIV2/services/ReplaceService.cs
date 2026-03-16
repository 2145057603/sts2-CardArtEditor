namespace InfectionCardReplacer.UIV2.Services;

internal sealed class ReplaceService
{
    public string ApplyReplacement(string cardId, string imagePath)
    {
        return CardArtOverrideRegistry.SetOverride(cardId, imagePath);
    }

    public string ResetReplacement(string cardId)
    {
        return CardArtOverrideRegistry.RemoveOverride(cardId);
    }

    public string ResetAllReplacements()
    {
        return CardArtOverrideRegistry.RemoveAllOverrides();
    }
}
