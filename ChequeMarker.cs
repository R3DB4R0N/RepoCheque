using UnityEngine;

namespace RepoCheque;

/// <summary>
/// Tags an object as "this is our cheque".
/// Every other feature (Rugrat immunity, indestructibility) tests for this component
/// instead of guessing from prefab names.
/// </summary>
internal class ChequeMarker : MonoBehaviour
{
    /// <summary>True once the visuals have been built successfully.</summary>
    internal bool VisualsReady;

    /// <summary>World-space size of the card: x = width, y = height, z = thickness.</summary>
    internal Vector3 CardSize = Vector3.one;

    /// <summary>The generated card object, used to hang the printed amount off.</summary>
    internal Transform? Card;

    /// <summary>Card size in the card transform's own local units (mesh dimensions).</summary>
    internal Vector3 CardLocalSize = Vector3.one;

    private PhysGrabObjectImpactDetector? _impact;

    private void Awake() => _impact = GetComponentInChildren<PhysGrabObjectImpactDetector>();

    private void Update()
    {
        if (_impact == null || !ChequeConfig.Indestructible.Value) return;

        // SurplusValuable.Update() releases the bag's 3-second spawn protection
        // (destroyDisable = false). We simply never let that happen, using the game's own
        // indestructibility flags rather than inventing a new mechanism.
        _impact.isIndestructible = true;
        _impact.destroyDisable = true;

        // ...but keep the impact sounds and particles, so it still feels like a real object.
        _impact.indestructibleBreakEffects = true;
    }

    /// <summary>
    /// Finds the marker from any collider/child that a game system happens to hand us.
    /// Returns null for ordinary loot.
    /// </summary>
    internal static ChequeMarker? Find(Component? c)
    {
        if (c == null) return null;
        return c.GetComponentInParent<ChequeMarker>() ?? c.GetComponentInChildren<ChequeMarker>();
    }

    internal static bool Is(Component? c) => Find(c) != null;
}
