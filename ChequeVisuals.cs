using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RepoCheque;

/// <summary>
/// THE VISUAL SEAM.
///
/// Everything that decides what the cheque *looks like* lives here: the generated card
/// mesh, the material, and the collider reshape. Rugrat immunity, cart spawning and
/// indestructibility deliberately live elsewhere and only depend on <see cref="ChequeMarker"/>.
///
/// If you ever build a real Unity prefab, replace <see cref="BuildChequeMesh"/> and
/// <see cref="Apply"/> and nothing else in the mod needs to change.
/// </summary>
internal static class ChequeVisuals
{
    /// <summary>A real cheque is roughly 2.2 times wider than it is tall.</summary>
    internal const float DefaultAspectRatio = 2.2f;

    private static float _cachedUpgradePackSize = -1f;

    internal static bool Apply(GameObject root)
    {
        if (root == null) return false;
        if (root.GetComponent<ChequeMarker>() != null) return true; // already converted

        var marker = root.AddComponent<ChequeMarker>();
        DumpHierarchy(root);

        // ---- how big should the card be? ---------------------------------------
        float aspect = ResolveAspectRatio();
        float packSize = GetUpgradePackSize();
        float width = packSize * 2f * ChequeConfig.ScaleMultiplier.Value;
        if (ChequeConfig.FitToCart.Value) width = ClampToCart(width, aspect);
        float height = width / aspect;
        float visualThickness = Mathf.Max(height * ChequeConfig.Thickness.Value, 0.004f);

        // The physics box is deliberately THICKER than the paper looks. A collider as thin
        // as the visual leaves the card's underside exactly coplanar with the floor, which
        // z-fights and flickers, and buries it inside a cart floor. Keeping the box a little
        // fatter makes the paper rest just above whatever it lands on.
        float physicsThickness = Mathf.Max(ChequeConfig.ColliderThickness.Value, visualThickness);

        var cardSize = new Vector3(width, height, visualThickness);
        marker.CardSize = cardSize;

        Log($"Cheque {width:0.000} x {height:0.000}, paper {visualThickness:0.000} thick, " +
            $"physics box {physicsThickness:0.000} thick (aspect {aspect:0.00}, pack ref {packSize:0.000})");

        int visualLayer = FindVisualLayer(root);
        Material? donor = HideOriginalRenderers(root);

        // ---- reshape physics FIRST so the card can be centred on the real body ---
        BoxCollider? main = ReshapeColliders(root, new Vector3(width, height, physicsThickness));

        try
        {
            BuildCardObject(root, main, cardSize, donor, visualLayer);
        }
        catch (System.Exception e)
        {
            RepoCheque.Logger.LogError($"Failed to build the cheque mesh: {e}");
            return false;
        }

        RefreshGrabGeometry(root);
        TuneRigidbody(root, main);

        // ---- make the Rugrat's own size filter skip us (see Phase 1-C) ----------
        var valuable = root.GetComponent<ValuableObject>();
        if (valuable != null) valuable.volumeType = ValuableVolume.Type.Wide;

        ChequeAmount.Attach(root, marker);
        ChequeCartProbe.Attach(root, marker);

        marker.VisualsReady = true;
        return true;
    }

    // ------------------------------------------------------------------------
    // Sizing
    // ------------------------------------------------------------------------

    /// <summary>Matches the mesh to the artwork so nothing gets stretched.</summary>
    private static float ResolveAspectRatio()
    {
        float over = ChequeConfig.AspectRatioOverride.Value;
        if (over > 0.01f) return over;

        Texture2D front = ChequeTextures.Front;
        if (front != null && front.height > 0)
        {
            float a = (float)front.width / front.height;
            if (a >= 1.0f && a <= 5f) return a;
        }
        return DefaultAspectRatio;
    }

    /// <summary>
    /// Shrinks the cheque if it wouldn't lie flat inside a cart.
    ///
    /// A card wider than the cart's interior can't settle: the solver keeps pushing it
    /// against the walls until it squeezes through the cart geometry and disappears from
    /// view. PhysGrabCart's own "In Cart" box (used by the game at PhysGrabCart.cs:167 as
    /// Physics.OverlapBox(inCart.position, inCart.localScale / 2f, ...)) tells us the real
    /// interior size, so we just make sure we fit inside it.
    /// </summary>
    private static float ClampToCart(float width, float aspect)
    {
        var carts = Object.FindObjectsOfType<PhysGrabCart>();
        if (carts == null || carts.Length == 0)
        {
            Log("No cart in the level to size against - leaving the cheque at its natural size.");
            return width;
        }

        float smallestX = float.MaxValue, smallestZ = float.MaxValue;
        foreach (var cart in carts)
        {
            if (cart == null || cart.inCart == null) continue;
            Vector3 inner = cart.inCart.lossyScale;
            RepoCheque.Logger.LogInfo(
                $"Cart '{cart.name}' interior = {inner} (small cart: {cart.isSmallCart}).");
            smallestX = Mathf.Min(smallestX, Mathf.Abs(inner.x));
            smallestZ = Mathf.Min(smallestZ, Mathf.Abs(inner.z));
        }

        if (smallestX == float.MaxValue) return width;

        // Lying flat the cheque occupies width x (width / aspect) on the cart floor.
        // Leave a margin so it drops in cleanly instead of wedging.
        const float margin = 0.85f;
        float maxByX = smallestX * margin;
        float maxByZ = smallestZ * margin * aspect;
        float allowed = Mathf.Min(maxByX, maxByZ);

        if (width <= allowed) return width;

        RepoCheque.Logger.LogInfo(
            $"Cheque narrowed from {width:0.000} to {allowed:0.000} so it lies flat inside the cart.");
        return allowed;
    }

    private static float GetUpgradePackSize()
    {
        if (_cachedUpgradePackSize > 0f) return _cachedUpgradePackSize;

        const float fallback = 0.35f;
        float measured = MeasureFromItemRegistry();
        if (measured > 0f) return _cachedUpgradePackSize = measured;

        measured = MeasureFromResources();
        if (measured > 0f) return _cachedUpgradePackSize = measured;

        RepoCheque.Logger.LogWarning(
            $"Could not measure an upgrade pack - using fallback {fallback}. " +
            "The cheque size is still fine; tune ScaleMultiplier in the config if you want it different.");
        return _cachedUpgradePackSize = fallback;
    }

    private static float MeasureFromItemRegistry()
    {
        try
        {
            var stats = StatsManager.instance;
            if (stats?.itemDictionary == null || stats.itemDictionary.Count == 0) return -1f;

            if (ChequeConfig.DebugLogging.Value)
                Log($"itemDictionary has {stats.itemDictionary.Count} entries, e.g. " +
                    string.Join(", ", stats.itemDictionary.Keys.Take(6)));

            foreach (var kvp in stats.itemDictionary)
            {
                if (kvp.Key.IndexOf("upgrade", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                GameObject? prefab = kvp.Value?.prefab?.Prefab;
                if (prefab == null) continue;

                float size = LargestDimension(prefab);
                if (size <= 0f) continue;

                RepoCheque.Logger.LogInfo($"Measured upgrade pack '{kvp.Key}': largest dimension {size:0.000} units.");
                return size;
            }
        }
        catch (System.Exception e) { Log($"Item-registry measurement failed: {e.Message}"); }

        return -1f;
    }

    private static float MeasureFromResources()
    {
        string[] paths =
        {
            "Items/Item Upgrade Player Health",
            "Items/Item Upgrade Player Energy",
            "Item Upgrade Player Health",
        };

        foreach (string p in paths)
        {
            try
            {
                var prefab = Resources.Load<GameObject>(p);
                if (prefab == null) continue;

                float size = LargestDimension(prefab);
                if (size <= 0f) continue;

                RepoCheque.Logger.LogInfo($"Measured upgrade pack via Resources '{p}': {size:0.000} units.");
                return size;
            }
            catch { /* try the next path */ }
        }
        return -1f;
    }

    private static float LargestDimension(GameObject prefab)
    {
        Bounds? b = null;

        foreach (var col in prefab.GetComponentsInChildren<Collider>(includeInactive: true))
        {
            if (col.isTrigger) continue;
            if (col is MeshCollider mc && !mc.convex) continue;
            b = b.HasValue ? Grow(b.Value, col.bounds) : col.bounds;
        }

        if (!b.HasValue)
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
                b = b.HasValue ? Grow(b.Value, r.bounds) : r.bounds;

        if (!b.HasValue) return -1f;
        Vector3 s = b.Value.size;
        return Mathf.Max(s.x, Mathf.Max(s.y, s.z));
    }

    private static Bounds Grow(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

    // ------------------------------------------------------------------------
    // Renderers
    // ------------------------------------------------------------------------

    private static Material? HideOriginalRenderers(GameObject root)
    {
        Material? donor = null;
        int hidden = 0;

        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
        {
            if (mr.gameObject.name == CardObjectName) continue;
            donor ??= mr.sharedMaterial;
            mr.enabled = false;
            hidden++;
        }

        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
        {
            donor ??= smr.sharedMaterial;
            smr.enabled = false;
            hidden++;
        }

        Log($"Hid {hidden} original renderer(s); donor shader = {(donor != null ? donor.shader.name : "none")}");
        return donor;
    }

    // ------------------------------------------------------------------------
    // The card
    // ------------------------------------------------------------------------

    private const string CardObjectName = "RepoCheque_Card";

    /// <summary>
    /// Builds the card and parents it to the physics collider itself, at the collider's
    /// centre. REPO pivots are not at object centres (that is exactly why the game keeps
    /// PhysGrabObject.midPointOffset), so anchoring to the collider is what guarantees the
    /// visible card and the solid body occupy the same space.
    /// </summary>
    private static void BuildCardObject(GameObject root, BoxCollider? main, Vector3 cardSize,
                                        Material? donor, int layer)
    {
        // Parent to the ROOT, not to the collider's own transform. Those collider transforms
        // carry heavy non-uniform scaling (measured: 0.34 / 0.14 / 0.33), which would both skew
        // the card and squash any text parented under it. Instead we sit on the root and simply
        // position ourselves at the collider's centre in world terms.
        Vector3 worldCentre = main != null
            ? main.transform.TransformPoint(main.center)
            : root.transform.position;

        var card = new GameObject(CardObjectName);
        card.transform.SetParent(root.transform, worldPositionStays: false);
        card.transform.localPosition = root.transform.InverseTransformPoint(worldCentre);
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one;

        // Match the layer of the mesh we replaced, not the collider's layer. The bag's visible
        // Mesh sits on layer 0 while its colliders are on 16, and rendering is layer-sensitive.
        card.layer = layer;

        Vector3 rs = root.transform.lossyScale;
        var meshSize = new Vector3(
            cardSize.x / Mathf.Max(Mathf.Abs(rs.x), 0.0001f),
            cardSize.y / Mathf.Max(Mathf.Abs(rs.y), 0.0001f),
            cardSize.z / Mathf.Max(Mathf.Abs(rs.z), 0.0001f));

        bool cutout = ChequeTextures.HasTransparency(ChequeTextures.Front);
        card.AddComponent<MeshFilter>().sharedMesh =
            BuildChequeMesh(meshSize.x, meshSize.y, meshSize.z, includeEdges: !cutout);

        var mr = card.AddComponent<MeshRenderer>();
        Material front = MakeMaterial(donor, ChequeTextures.Front);
        Material back = ChequeTextures.Back != null ? MakeMaterial(donor, ChequeTextures.Back) : front;
        mr.sharedMaterials = new[] { front, back };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;

        var marker = root.GetComponent<ChequeMarker>();
        if (marker != null)
        {
            marker.Card = card.transform;
            marker.CardLocalSize = meshSize;
        }

        Log($"Card parented to root at local {card.transform.localPosition}, " +
            $"layer {layer}, mesh size {meshSize}.");
    }

    /// <summary>The layer the bag's visible mesh used, so our card renders the same way.</summary>
    private static int FindVisualLayer(GameObject root)
    {
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
            if (mr.enabled && mr.gameObject.name != CardObjectName)
                return mr.gameObject.layer;

        return 0; // Default
    }

    private static Vector3 ScaleFactor(Transform root, Transform child)
    {
        Vector3 r = root.lossyScale, c = child.lossyScale;
        return new Vector3(
            Mathf.Approximately(r.x, 0f) ? 1f : Mathf.Max(Mathf.Abs(c.x / r.x), 0.0001f),
            Mathf.Approximately(r.y, 0f) ? 1f : Mathf.Max(Mathf.Abs(c.y / r.y), 0.0001f),
            Mathf.Approximately(r.z, 0f) ? 1f : Mathf.Max(Mathf.Abs(c.z / r.z), 0.0001f));
    }

    /// <summary>
    /// Builds the flat card: a thin box, 12 triangles, 24 vertices.
    /// Submesh 0 = front face plus the four edges, submesh 1 = the reverse face.
    /// </summary>
    internal static Mesh BuildChequeMesh(float width, float height, float thickness, bool includeEdges = true)
    {
        float hw = width * 0.5f, hh = height * 0.5f, hd = thickness * 0.5f;

        var verts = new List<Vector3>(24);
        var norms = new List<Vector3>(24);
        var uvs = new List<Vector2>(24);
        var faceTris = new List<int>(30);
        var backTris = new List<int>(6);

        // Edges sample the middle of the artwork, which is reliably opaque paper.
        // (The corners are transparent on torn-paper artwork, which would erase the edges.)
        var edgeUv = new Vector2(0.5f, 0.5f);

        void Quad(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Vector3 n,
                  Vector2 uvBl, Vector2 uvBr, Vector2 uvTr, Vector2 uvTl, List<int> tris)
        {
            int i = verts.Count;
            verts.Add(bl); verts.Add(br); verts.Add(tr); verts.Add(tl);
            for (int k = 0; k < 4; k++) norms.Add(n);
            uvs.Add(uvBl); uvs.Add(uvBr); uvs.Add(uvTr); uvs.Add(uvTl);
            // Winding: bl -> br -> tr -> tl as seen from outside the face.
            // (The reverse order renders each quad from the wrong side, which put the
            // printed amount behind the artwork instead of on top of it.)
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        // U runs right-to-left across the quad corners on purpose. Unity is left-handed, so
        // when you stand outside a face and look at it, that face's +X axis runs to your
        // LEFT. Mapping u=0 to the -X corner therefore prints the artwork mirrored.
        Vector2 uvBl = new(1, 0), uvBr = new(0, 0), uvTr = new(0, 1), uvTl = new(1, 1);

        // Front (+Z): the printed face.
        Quad(new Vector3(-hw, -hh, hd), new Vector3(hw, -hh, hd),
             new Vector3(hw, hh, hd), new Vector3(-hw, hh, hd), Vector3.forward,
             uvBl, uvBr, uvTr, uvTl, faceTris);

        // Back (-Z): its own screen space, so a back image reads correctly from behind.
        Quad(new Vector3(hw, -hh, -hd), new Vector3(-hw, -hh, -hd),
             new Vector3(-hw, hh, -hd), new Vector3(hw, hh, -hd), Vector3.back,
             uvBl, uvBr, uvTr, uvTl, backTris);

        // Edge faces are skipped for cut-out artwork. The front/back are clipped to the
        // paper's torn silhouette, but the four edges are solid rectangles - leaving them
        // in draws a hard rectangular rim around the ragged paper.
        if (includeEdges)
        {
            Quad(new Vector3(hw, -hh, hd), new Vector3(hw, -hh, -hd),
                 new Vector3(hw, hh, -hd), new Vector3(hw, hh, hd), Vector3.right,
                 edgeUv, edgeUv, edgeUv, edgeUv, faceTris);

            Quad(new Vector3(-hw, -hh, -hd), new Vector3(-hw, -hh, hd),
                 new Vector3(-hw, hh, hd), new Vector3(-hw, hh, -hd), Vector3.left,
                 edgeUv, edgeUv, edgeUv, edgeUv, faceTris);

            Quad(new Vector3(hw, hh, -hd), new Vector3(-hw, hh, -hd),
                 new Vector3(-hw, hh, hd), new Vector3(hw, hh, hd), Vector3.up,
                 edgeUv, edgeUv, edgeUv, edgeUv, faceTris);

            Quad(new Vector3(-hw, -hh, -hd), new Vector3(hw, -hh, -hd),
                 new Vector3(hw, -hh, hd), new Vector3(-hw, -hh, hd), Vector3.down,
                 edgeUv, edgeUv, edgeUv, edgeUv, faceTris);
        }

        var mesh = new Mesh { name = "RepoChequeCard" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(faceTris, 0);
        mesh.SetTriangles(backTris, 1);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material MakeMaterial(Material? donor, Texture2D tex)
    {
        Material m = donor != null
            ? new Material(donor)
            : new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));

        m.name = "RepoChequeMaterial";

        foreach (string prop in new[] { "_AlbedoTexture", "_MainTex", "_BaseMap", "_BaseColorMap" })
            if (m.HasProperty(prop)) m.SetTexture(prop, tex);

        foreach (string prop in new[] { "_AlbedoColor", "_Color", "_BaseColor" })
            if (m.HasProperty(prop)) m.SetColor(prop, Color.white);

        if (m.HasProperty("_ColorOverlayAmount")) m.SetFloat("_ColorOverlayAmount", 0f);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        if (m.HasProperty("_METALLIC_ON")) m.SetFloat("_METALLIC_ON", 0f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);

        // Artwork with ragged/torn paper edges carries an alpha channel. Switching the
        // material to alpha cutout makes the cheque take the paper's real silhouette
        // instead of drawing a hard rectangle with dead corners.
        if (ChequeTextures.HasTransparency(tex))
        {
            m.SetOverrideTag("RenderType", "TransparentCutout");
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            RepoCheque.Logger.LogInfo($"'{tex.name}' has transparency - using alpha cutout so the torn edges show.");
        }

        return m;
    }

    // ------------------------------------------------------------------------
    // Physics
    // ------------------------------------------------------------------------

    /// <summary>
    /// Reshapes the bag's grab collider into the card.
    ///
    /// Only colliders carrying PhysGrabObjectCollider are candidates: PhysGrabObject builds
    /// its grabbable list from exactly those (PhysGrabObject.cs:495), so reshaping anything
    /// else would leave the cheque looking right but impossible to pick up.
    /// Never adds a MeshCollider - REPO's phys-grab system can't use one.
    /// </summary>
    private static BoxCollider? ReshapeColliders(GameObject root, Vector3 cardSize)
    {
        var grabColliders = new List<Collider>();
        foreach (var pgc in root.GetComponentsInChildren<PhysGrabObjectCollider>(includeInactive: true))
            grabColliders.AddRange(pgc.GetComponents<Collider>());

        var grabBoxes = grabColliders.OfType<BoxCollider>().Where(b => !b.isTrigger).ToList();

        if (grabBoxes.Count == 0)
        {
            RepoCheque.Logger.LogWarning(
                "No grabbable BoxCollider found on the surplus valuable - leaving its physics alone. " +
                "The cheque will look right but keep the bag's original shape.");
            return null;
        }

        BoxCollider main = grabBoxes.OrderByDescending(b => b.size.x * b.size.y * b.size.z).First();

        // Align the box with the root so it matches the card, whatever the nested
        // collider transforms are doing.
        main.transform.rotation = root.transform.rotation;

        Vector3 f = ScaleFactor(root.transform, main.transform);
        main.size = new Vector3(cardSize.x / f.x, cardSize.y / f.y, cardSize.z / f.z);
        main.contactOffset = 0.01f; // Unity's default; the box is now thick enough for it

        int disabled = 0;
        foreach (var c in grabColliders)
        {
            if (c == main || c.isTrigger || !c.enabled) continue;
            c.enabled = false;
            disabled++;
        }

        Log($"Reshaped grab collider '{main.name}' to {main.size} (contactOffset {main.contactOffset:0.0000}); " +
            $"disabled {disabled} other grab collider(s) out of {grabColliders.Count}.");

        return main;
    }

    /// <summary>
    /// Recomputes the grab midpoint after reshaping, so the game grabs the card's centre
    /// rather than wherever the money bag's middle used to be.
    /// </summary>
    private static void RefreshGrabGeometry(GameObject root)
    {
        var pgo = root.GetComponent<PhysGrabObject>();
        if (pgo == null) return;

        Bounds? b = null;
        foreach (var c in root.GetComponentsInChildren<Collider>(includeInactive: false))
        {
            if (c.isTrigger || !c.enabled) continue;
            b = b.HasValue ? Grow(b.Value, c.bounds) : c.bounds;
        }
        if (!b.HasValue) return;

        pgo.midPointOffset = root.transform.InverseTransformPoint(b.Value.center);
        pgo.centerPoint = b.Value.center;
        Log($"Grab midpoint moved to local {pgo.midPointOffset}.");
    }

    /// <summary>Paper weight, and settings that stop a thin slab from juddering.</summary>
    private static void TuneRigidbody(GameObject root, BoxCollider? main)
    {
        var rb = root.GetComponent<Rigidbody>();
        if (rb == null) return;

        float wanted = ChequeConfig.Mass.Value;
        if (wanted > 0f)
        {
            var pgo = root.GetComponent<PhysGrabObject>();
            Log($"Mass {rb.mass:0.00} -> {wanted:0.00} (the bag's weight scaled with the surplus; paper shouldn't).");
            rb.mass = wanted;
            // PhysGrabObject restores rb.mass from massOriginal (PhysGrabObject.cs:1563),
            // so both have to change or the weight snaps back.
            if (pgo != null) pgo.massOriginal = wanted;
        }

        rb.angularDrag = Mathf.Max(rb.angularDrag, 2.5f);
        rb.maxAngularVelocity = Mathf.Min(rb.maxAngularVelocity, 12f);
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    // ------------------------------------------------------------------------
    // Diagnostics
    // ------------------------------------------------------------------------

    private static void DumpHierarchy(GameObject root)
    {
        if (!ChequeConfig.DebugLogging.Value) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- surplus valuable '{root.name}' hierarchy ---");

        void Walk(Transform t, int depth)
        {
            string pad = new string(' ', depth * 2);
            var bits = new List<string>();
            foreach (var c in t.GetComponents<Component>())
                if (c != null && c is not Transform) bits.Add(c.GetType().Name);

            var col = t.GetComponent<Collider>();
            string colInfo = col switch
            {
                BoxCollider bc => $" box size={bc.size} center={bc.center}",
                null => "",
                _ => $" {col.GetType().Name}",
            };

            sb.AppendLine($"{pad}{t.name} [layer {t.gameObject.layer}] local={t.localPosition} " +
                          $"rot={t.localRotation.eulerAngles} lossyScale={t.lossyScale}{colInfo}");
            sb.AppendLine($"{pad}   {string.Join(", ", bits)}");

            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), depth + 1);
        }

        Walk(root.transform, 0);
        RepoCheque.Logger.LogInfo(sb.ToString());
    }

    private static void Log(string msg)
    {
        if (ChequeConfig.DebugLogging.Value) RepoCheque.Logger.LogInfo(msg);
    }
}
