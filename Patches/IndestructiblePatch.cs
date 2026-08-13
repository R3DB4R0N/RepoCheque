using HarmonyLib;
using UnityEngine;

namespace RepoCheque.Patches;

/// <summary>
/// F5 - the cheque never loses value.
///
/// PhysGrabObjectImpactDetector.Break() is the single choke point: BreakLight, BreakMedium
/// and BreakHeavy all compute a valueLost and funnel into it, and it alone decides whether
/// the value actually drops:
///
///     if (isValuable &amp;&amp; (_forceBreak || (!isIndestructible &amp;&amp; !destroyDisable))) flag = true;
///
/// That flag becomes _loseValue in BreakRPC. Zeroing valueLost and clearing _forceBreak
/// means no money is lost and the &lt;15% destroy branch is never reached - while
/// indestructibleBreakEffects (kept true by ChequeMarker) lets the impact sounds and
/// particles play exactly as before. Paper doesn't shatter, but it still feels physical.
/// </summary>
[HarmonyPatch(typeof(PhysGrabObjectImpactDetector))]
internal static class IndestructiblePatch
{
    [HarmonyPrefix]
    [HarmonyPatch("Break")]
    private static void Break_Prefix(PhysGrabObjectImpactDetector __instance,
                                     ref float valueLost, ref bool _forceBreak)
    {
        if (!ChequeConfig.Indestructible.Value) return;
        if (!ChequeMarker.Is(__instance)) return;

        valueLost = 0f;
        _forceBreak = false;
        __instance.isIndestructible = true;
        __instance.destroyDisable = true;
    }

    /// <summary>Belt and braces: nothing gets to destroy the cheque outright either.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(nameof(PhysGrabObjectImpactDetector.DestroyObject))]
    private static bool DestroyObject_Prefix(PhysGrabObjectImpactDetector __instance)
    {
        if (!ChequeConfig.Indestructible.Value) return true;
        if (!ChequeMarker.Is(__instance)) return true;

        return false; // skip the original method entirely
    }
}
