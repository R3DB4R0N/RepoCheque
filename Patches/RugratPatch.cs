using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace RepoCheque.Patches;

/// <summary>
/// F3 - the Rugrat never targets, picks up or throws the cheque.
///
/// "Rugrat" is the community name; in code the enemy is EnemyValuableThrower (confirmed by
/// pairing the asset lists "Rugrat -> Bowtie" and "Enemy - Valuable Thrower -> Enemy - Bowtie").
///
/// Two layers of defence:
///   1. ChequeVisuals sets volumeType = Wide. StateGetValuable already skips anything above
///      Big, so vanilla logic filters the cheque out on its own.
///   2. This patch, which works off our marker instead of the size, so it still holds if the
///      volume type ever changes.
///
/// When nothing valid is left the enemy goes to State.Leave, which is the game's own
/// no-target path - it walks off calmly rather than erroring or freezing.
/// </summary>
[HarmonyPatch(typeof(EnemyValuableThrower))]
internal static class RugratPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("StateGetValuable")]
    private static void StateGetValuable_Postfix(EnemyValuableThrower __instance)
    {
        if (!ChequeConfig.Enabled.Value) return;

        PhysGrabObject target = __instance.valuableTarget;
        if (target == null || !ChequeMarker.Is(target)) return;

        PhysGrabObject? replacement = FindNextValuable(__instance);

        if (replacement != null)
        {
            // The original already moved us to GoToTarget; just swap what we walk towards.
            __instance.valuableTarget = replacement;
            Log($"Rugrat retargeted from the cheque to '{replacement.name}'.");
        }
        else
        {
            __instance.valuableTarget = null;
            __instance.UpdateState(EnemyValuableThrower.State.Leave);
            Log("Rugrat found only a cheque worth taking - leaving empty-handed.");
        }
    }

    /// <summary>
    /// Belt and braces: if a Rugrat somehow already holds the cheque - saved game, a state we
    /// didn't anticipate, another mod - drop it cleanly instead of letting it be carried off.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch("StateGoToTarget")]
    private static void StateGoToTarget_Postfix(EnemyValuableThrower __instance) => DropCheque(__instance);

    [HarmonyPostfix]
    [HarmonyPatch("StatePickUpTarget")]
    private static void StatePickUpTarget_Postfix(EnemyValuableThrower __instance) => DropCheque(__instance);

    private static void DropCheque(EnemyValuableThrower instance)
    {
        if (!ChequeConfig.Enabled.Value) return;

        PhysGrabObject target = instance.valuableTarget;
        if (target == null || !ChequeMarker.Is(target)) return;

        instance.valuableTarget = null;
        instance.UpdateState(EnemyValuableThrower.State.Leave);
        Log("Rugrat was holding onto a cheque - released it and moved on.");
    }

    /// <summary>
    /// Repeats the game's own search from StateGetValuable, skipping our cheque: nearest
    /// NavMesh-reachable valuable wins, otherwise the nearest unreachable one.
    /// </summary>
    private static PhysGrabObject? FindNextValuable(EnemyValuableThrower instance)
    {
        PlayerAvatar player = instance.playerTarget;
        if (player == null) return null;

        PhysGrabObject? reachable = null, unreachable = null;
        float bestReachable = float.MaxValue, bestUnreachable = float.MaxValue;

        Collider[] hits = Physics.OverlapSphere(player.transform.position, 10f,
                                                LayerMask.GetMask("PhysGrabObject"));

        foreach (Collider hit in hits)
        {
            var valuable = hit.GetComponentInParent<ValuableObject>();
            if (valuable == null) continue;
            if (valuable.volumeType > ValuableVolume.Type.Big) continue;
            if (ChequeMarker.Is(valuable)) continue;

            PhysGrabObject grab = valuable.physGrabObject;
            if (grab == null) continue;

            float dist = Vector3.Distance(player.transform.position, valuable.transform.position);

            if (NavMesh.SamplePosition(valuable.transform.position, out _, 1f, -1))
            {
                if (dist < bestReachable) { bestReachable = dist; reachable = grab; }
            }
            else if (dist < bestUnreachable) { bestUnreachable = dist; unreachable = grab; }
        }

        return reachable ?? unreachable;
    }

    private static void Log(string msg)
    {
        if (ChequeConfig.DebugLogging.Value) RepoCheque.Logger.LogInfo(msg);
    }
}
