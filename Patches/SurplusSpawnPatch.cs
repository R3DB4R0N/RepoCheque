using HarmonyLib;
using UnityEngine;

namespace RepoCheque.Patches;

/// <summary>
/// F4 - the cheque appears inside a nearby cart instead of dropping from the ceiling.
///
/// ExtractionPoint.SpawnTaxReturn() spawns at surplusSpawnTransform.position, on the master
/// client only. Rather than rewriting the spawn call (and its Photon networking), this moves
/// that transform to the chosen spot just before the game reads it and puts it straight back
/// afterwards. The game's own instantiate + sync therefore does all the work, unchanged.
///
/// Fallbacks, in order:
///   1. Nearest cart within CartSearchRadius -> centre of its interior, just above the floor.
///   2. No cart -> the extraction point, but lowered to just above the ground.
///   3. Anything unexpected -> leave vanilla behaviour completely alone.
/// </summary>
[HarmonyPatch(typeof(ExtractionPoint))]
internal static class SurplusSpawnPatch
{
    private static Vector3? _savedPosition;

    /// <summary>How far above the resting surface to release it, in metres.</summary>
    private const float DropClearance = 0.30f;

    [HarmonyPrefix]
    [HarmonyPatch("SpawnTaxReturn")]
    private static void SpawnTaxReturn_Prefix(ExtractionPoint __instance)
    {
        _savedPosition = null;

        try
        {
            if (!ChequeConfig.Enabled.Value || !ChequeConfig.SpawnInCart.Value) return;

            // Host-authoritative: the game only spawns on the master client, so only the
            // master decides where. Everyone else receives the position through Photon.
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
            if (RoundDirector.instance == null || RoundDirector.instance.extractionPointSurplus <= 0) return;

            Transform spawn = __instance.surplusSpawnTransform;
            if (spawn == null) return;

            Vector3? target = FindCartPoint(spawn.position) ?? FindGroundPoint(spawn.position);
            if (target == null)
            {
                Log("No cart and no ground found - keeping the vanilla spawn position.");
                return;
            }

            _savedPosition = spawn.position;
            spawn.position = target.Value;
            ChequeSpawnFlag.Arm();
        }
        catch (System.Exception e)
        {
            RepoCheque.Logger.LogError($"Cart spawn redirect failed, falling back to vanilla:\n{e}");
            _savedPosition = null;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("SpawnTaxReturn")]
    private static void SpawnTaxReturn_Postfix(ExtractionPoint __instance)
    {
        // Always put the extraction point's own transform back, whatever happened.
        if (_savedPosition.HasValue && __instance.surplusSpawnTransform != null)
            __instance.surplusSpawnTransform.position = _savedPosition.Value;

        _savedPosition = null;
    }

    private static Vector3? FindCartPoint(Vector3 near)
    {
        PhysGrabCart? best = null;
        float bestDist = ChequeConfig.CartSearchRadius.Value;

        foreach (var cart in Object.FindObjectsOfType<PhysGrabCart>())
        {
            if (cart == null || cart.inCart == null) continue;

            float d = Vector3.Distance(near, cart.transform.position);
            if (d > bestDist) continue;

            bestDist = d;
            best = cart;
        }

        if (best == null) return null;

        var box = best.inCart.GetComponent<BoxCollider>();
        Bounds interior = box != null
            ? box.bounds
            : new Bounds(best.inCart.position, best.inCart.lossyScale);

        // Centred over the cart floor with enough clearance that nothing is intersecting
        // at the instant it appears - overlapping colliders are what launch objects away.
        var point = new Vector3(interior.center.x, interior.min.y + DropClearance, interior.center.z);

        RepoCheque.Logger.LogInfo(
            $"Spawning the cheque inside cart '{best.name}' at {point} " +
            $"(interior {interior.size}, {bestDist:0.0}m from the extraction point).");

        return point;
    }

    /// <summary>No cart: drop it just above the floor under the extraction point instead.</summary>
    private static Vector3? FindGroundPoint(Vector3 from)
    {
        if (!Physics.Raycast(from + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 12f,
                             ~0, QueryTriggerInteraction.Ignore))
            return null;

        var point = hit.point + Vector3.up * DropClearance;
        RepoCheque.Logger.LogInfo($"No cart within {ChequeConfig.CartSearchRadius.Value}m - " +
                                  $"spawning the cheque low at the extraction point instead ({point}).");
        return point;
    }

    private static void Log(string msg)
    {
        if (ChequeConfig.DebugLogging.Value) RepoCheque.Logger.LogInfo(msg);
    }
}

/// <summary>
/// Bridges the spawn patch and the object itself: SpawnTaxReturn decides the position, but
/// the settling has to happen on the spawned GameObject, which only exists once Start runs.
/// </summary>
internal static class ChequeSpawnFlag
{
    private static float _armedUntil = -1f;

    internal static void Arm() => _armedUntil = Time.time + 3f;

    /// <summary>True if we redirected the most recent surplus spawn.</summary>
    internal static bool ConsumeIfArmed()
    {
        if (Time.time > _armedUntil) return false;
        _armedUntil = -1f;
        return true;
    }
}
