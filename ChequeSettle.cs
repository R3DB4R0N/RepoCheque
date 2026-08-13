using UnityEngine;

namespace RepoCheque;

/// <summary>
/// F4 (second half) - keeps the cheque still for a moment after it appears.
///
/// The game gives the surplus a random tumble (PhysGrabObject.spawnTorque = insideUnitSphere
/// * 0.05f) which is fine for a bag dropping from the ceiling but would flip a cheque out of
/// a cart. Rather than freezing the body kinematic - which upsets Photon's transform sync -
/// this just holds velocity at zero for a few physics steps, so it settles instead of popping.
///
/// Host-only: the master client owns the physics and the result syncs normally.
/// </summary>
internal class ChequeSettle : MonoBehaviour
{
    private Rigidbody? _rb;
    private PhysGrabObject? _grab;
    private float _timer;

    internal static void Attach(GameObject root, float seconds = 0.4f)
    {
        if (root.GetComponent<ChequeSettle>() != null) return;

        var s = root.AddComponent<ChequeSettle>();
        s._rb = root.GetComponent<Rigidbody>();
        s._grab = root.GetComponent<PhysGrabObject>();
        s._timer = seconds;

        // Lay it flat: the card's local +Z is the paper's normal, so pointing that at the
        // sky puts the printed face up.
        root.transform.rotation = Quaternion.Euler(-90f, Random.Range(-8f, 8f), 0f);

        if (s._grab != null) s._grab.spawnTorque = Vector3.zero;
        if (s._rb != null)
        {
            s._rb.velocity = Vector3.zero;
            s._rb.angularVelocity = Vector3.zero;
        }
    }

    private void FixedUpdate()
    {
        if (_timer <= 0f) { Destroy(this); return; }
        _timer -= Time.fixedDeltaTime;

        if (_grab != null) _grab.spawnTorque = Vector3.zero;
        if (_rb == null) return;

        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }
}
