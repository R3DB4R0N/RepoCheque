using UnityEngine;

namespace RepoCheque;

/// <summary>
/// Diagnostic only, and only while DebugLogging is on.
///
/// Nothing in R.E.P.O.'s cart code hides an object's renderers - I read PhysGrabCart,
/// PhysGrabInCart and PhysGrabObjectImpactDetector line by line. So when the cheque
/// "disappears" in a cart it is still being drawn; it is just somewhere we cannot see.
/// This reports the numbers needed to tell those cases apart:
///
///   renderer off        -> something is disabling it
///   isVisible = false   -> it is being culled
///   below the cart floor-> the cart's collision floor sits under its visible floor,
///                          so a flat object sinks out of sight (raise ColliderThickness)
///   edge-on rotation    -> a flat sheet viewed exactly side-on has no thickness to see
/// </summary>
internal class ChequeCartProbe : MonoBehaviour
{
    private PhysGrabObjectImpactDetector? _impact;
    private MeshRenderer? _renderer;
    private float _timer;
    private bool _wasInCart;

    internal static void Attach(GameObject root, ChequeMarker marker)
    {
        if (!ChequeConfig.CartProbeLogging.Value) return;
        if (root.GetComponent<ChequeCartProbe>() != null) return;

        var probe = root.AddComponent<ChequeCartProbe>();
        probe._impact = root.GetComponentInChildren<PhysGrabObjectImpactDetector>();
        probe._renderer = marker.Card != null ? marker.Card.GetComponent<MeshRenderer>() : null;
    }

    private void Update()
    {
        if (_impact == null || _renderer == null) return;

        bool inCart = _impact.inCart;

        if (inCart != _wasInCart)
        {
            _wasInCart = inCart;
            RepoCheque.Logger.LogInfo($"[cart probe] inCart = {inCart}");
            _timer = 0f;
        }

        if (!inCart) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = 1f;

        Bounds b = _renderer.bounds;
        Transform t = transform;

        // How edge-on is the card? The card's local +Z is the paper's normal, so a normal
        // pointing sideways means we are looking at a zero-thickness sheet from its edge.
        Transform cardT = _renderer.transform;
        float tilt = Mathf.Abs(Vector3.Dot(cardT.forward, Vector3.up));

        string cartInfo = "no cart found";
        PhysGrabCart? cart = FindNearestCart(t.position);
        if (cart != null && cart.inCart != null)
        {
            var box = cart.inCart.GetComponent<BoxCollider>();
            float floorY = box != null
                ? box.bounds.min.y
                : cart.inCart.position.y - cart.inCart.lossyScale.y * 0.5f;

            cartInfo = $"cartFloorY={floorY:0.000} chequeMinY={b.min.y:0.000} " +
                       $"deltaAboveFloor={(b.min.y - floorY):0.000} cartPos={cart.transform.position}";
        }

        RepoCheque.Logger.LogInfo(
            $"[cart probe] rendererEnabled={_renderer.enabled} isVisible={_renderer.isVisible} " +
            $"cardWorldPos={cardT.position} boundsSize={b.size} " +
            $"flatness={tilt:0.00} (1=lying flat, 0=edge-on) | {cartInfo}");
    }

    private static PhysGrabCart? FindNearestCart(Vector3 from)
    {
        PhysGrabCart? best = null;
        float bestDist = float.MaxValue;

        foreach (var c in Object.FindObjectsOfType<PhysGrabCart>())
        {
            if (c == null) continue;
            float d = Vector3.Distance(from, c.transform.position);
            if (d < bestDist) { bestDist = d; best = c; }
        }

        return best;
    }
}
