using Godot;
using HarmonyLib;
using InfectionCardReplacer.UIV2;
using MegaCrit.Sts2.Core.Modding;

namespace InfectionCardReplacer;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    internal const string ModId = "InfectionCardReplacer";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Harmony harmony = new(ModId);

        harmony.PatchAll();
        GD.Print("InfectionCardReplacer initialized.");
        SkinCenterRoot.EnsureInstalledOnRoot();
    }
}
