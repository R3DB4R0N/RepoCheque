using HarmonyLib;

namespace RepoCheque.Patches;

/// <summary>
/// F1 - turns the surplus money bag into a cheque.
///
/// SurplusValuable is a plain MonoBehaviour sitting on the three surplus prefabs
/// (Small/Medium/Big) and nothing else, so its Start() is both a reliable "this is the
/// surplus one" test AND a per-client hook: it runs on every machine that sees the
/// object, including players who spawned it remotely. That is what keeps the re-skin
/// consistent in multiplayer without any networking of our own.
/// </summary>
[HarmonyPatch(typeof(SurplusValuable))]
internal static class SurplusValuablePatch
{
    [HarmonyPostfix]
    [HarmonyPatch("Start")]
    private static void Start_Postfix(SurplusValuable __instance)
    {
        if (!ChequeConfig.Enabled.Value) return;

        try
        {
            if (ChequeVisuals.Apply(__instance.gameObject))
                RepoCheque.Logger.LogInfo($"Surplus valuable re-skinned as a cheque ({__instance.gameObject.name}).");
            else
                RepoCheque.Logger.LogWarning("Cheque re-skin did not complete; the vanilla bag may still be visible.");

            // F4: only the host settles it, and only if we actually redirected this spawn.
            // Everyone else receives the resting position through the game's normal sync.
            if (SemiFunc.IsMasterClientOrSingleplayer() && ChequeSpawnFlag.ConsumeIfArmed())
                ChequeSettle.Attach(__instance.gameObject);
        }
        catch (System.Exception e)
        {
            // Never let our cosmetic code break the extraction.
            RepoCheque.Logger.LogError($"Cheque re-skin threw an exception, leaving the vanilla bag alone:\n{e}");
        }
    }
}
