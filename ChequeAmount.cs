using System.Linq;
using TMPro;
using UnityEngine;

namespace RepoCheque;

/// <summary>
/// F2 - draws the money value on the cheque face.
///
/// The value is NOT ready when the object spawns: ValuableObject.Start() kicks off a
/// DollarValueSet() coroutine that waits for level generation, then for a valid Photon ID,
/// and only then does the host set the value and RPC it out. So we wait for the game's own
/// dollarValueSet flag rather than printing $0. Every client reads its own local copy -
/// this mod adds no networking of its own.
/// </summary>
internal class ChequeAmount : MonoBehaviour
{
    private ValuableObject? _valuable;
    private TextMeshPro[] _texts = System.Array.Empty<TextMeshPro>();
    private float _lastDrawn = float.NaN;
    private float _retryTimer;

    private static TMP_FontAsset? _gameFont;
    private static bool _fontSearched;

    internal static void Attach(GameObject root, ChequeMarker marker)
    {
        if (!ChequeConfig.ShowPrintedAmount.Value) return;
        if (marker.Card == null) return;
        if (root.GetComponent<ChequeAmount>() != null) return;

        root.AddComponent<ChequeAmount>().Build(marker);
    }

    private void Build(ChequeMarker marker)
    {
        _valuable = GetComponent<ValuableObject>();
        Vector3 size = marker.CardLocalSize;

        string side = ChequeConfig.TextSide.Value.Trim().ToLowerInvariant();
        bool wantFront = side is "front" or "both";
        bool wantBack = side is "back" or "both";
        if (!wantFront && !wantBack) wantFront = true; // unrecognised value -> sane default

        var made = new System.Collections.Generic.List<TextMeshPro>(2);
        if (wantFront) made.Add(MakeText(marker, size, onFront: true));
        if (wantBack) made.Add(MakeText(marker, size, onFront: false));
        _texts = made.Where(t => t != null).ToArray()!;

        Refresh();
    }

    private TextMeshPro MakeText(ChequeMarker marker, Vector3 size, bool onFront)
    {
        var go = new GameObject(onFront ? "RepoCheque_Amount_Front" : "RepoCheque_Amount_Back");
        go.transform.SetParent(marker.Card, worldPositionStays: false);
        go.layer = marker.Card!.gameObject.layer;

        // Add the component FIRST. TextMeshPro requires a RectTransform, and adding it
        // replaces the plain Transform - so anything positioned beforehand is thrown away.
        var text = go.AddComponent<TextMeshPro>();
        var rt = text.rectTransform;

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size.x * ChequeConfig.TextBoxWidth.Value,
                                   size.y * ChequeConfig.TextBoxHeight.Value);

        // Config offsets are 0..1 across the artwork; convert to card-local units.
        // The sign matches the mesh's mirrored U mapping (see ChequeVisuals.BuildChequeMesh),
        // so the number lands on the same spot of the artwork the offsets describe.
        float u = ChequeConfig.TextOffsetU.Value;
        float x = (0.5f - u) * size.x;
        float y = (ChequeConfig.TextOffsetV.Value - 0.5f) * size.y;
        float depth = size.z * 0.5f + ChequeConfig.TextDepthOffset.Value;

        // A TextMeshPro with no rotation is legible to a camera looking along +Z - that is,
        // from BEHIND the card's front face. Reading it from the front side therefore needs
        // the 180 degree turn, and the reverse face needs none. Same left-handed quirk that
        // mirrored the artwork's UVs.
        if (onFront)
        {
            rt.localPosition = new Vector3(x, y, depth);
            rt.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
        else
        {
            rt.localPosition = new Vector3(-x, y, -depth);
            rt.localRotation = Quaternion.identity;
        }

        rt.localScale = Vector3.one;

        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.color = ChequeConfig.GetTextColor();
        text.text = string.Empty;

        TMP_FontAsset? font = GetGameFont();
        if (font != null) text.font = font;

        if (ChequeConfig.TextAutoFit.Value)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.01f;
            text.fontSizeMax = ChequeConfig.TextSize.Value;
        }
        else
        {
            text.enableAutoSizing = false;
            text.fontSize = ChequeConfig.TextSize.Value;
        }

        Log($"Amount text ({(onFront ? "front" : "back")}) at local {rt.localPosition}, " +
            $"box {rt.sizeDelta}, font '{(font != null ? font.name : "TMP default")}'.");

        return text;
    }

    /// <summary>
    /// Reuses the game's own Teko font asset so the printed amount matches every other
    /// number in R.E.P.O. The game ships "Teko-VariableFont_wght SDF", so there is nothing
    /// to bundle and nothing for the player to install.
    /// </summary>
    private static TMP_FontAsset? GetGameFont()
    {
        if (_fontSearched) return _gameFont;
        _fontSearched = true;

        try
        {
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

            _gameFont = all.FirstOrDefault(f =>
                            f != null && f.name.IndexOf("teko", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        ?? TMP_Settings.defaultFontAsset
                        ?? all.FirstOrDefault(f => f != null);

            if (_gameFont != null)
                RepoCheque.Logger.LogInfo($"Using game font '{_gameFont.name}' for the printed amount.");
            else
                RepoCheque.Logger.LogWarning("No TextMeshPro font asset found - falling back to the built-in font.");
        }
        catch (System.Exception e)
        {
            RepoCheque.Logger.LogWarning($"Font lookup failed ({e.Message}) - using the built-in font.");
        }

        return _gameFont;
    }

    private void Update()
    {
        if (_texts.Length == 0 || _valuable == null) return;

        _retryTimer -= Time.deltaTime;
        if (_retryTimer > 0f) return;
        _retryTimer = 0.25f;

        Refresh();
    }

    private void Refresh()
    {
        if (_texts.Length == 0 || _valuable == null) return;

        if (!_valuable.dollarValueSet)
        {
            foreach (var t in _texts) t.text = string.Empty; // never print $0 before the value arrives
            return;
        }

        float value = _valuable.dollarValueCurrent;
        if (Mathf.Approximately(value, _lastDrawn)) return;
        _lastDrawn = value;

        // The game's own formatter, so the cheque matches every other money readout.
        // Note this is comma-grouped ("12,500"), not "$12.5K".
        string drawn = "$" + SemiFunc.DollarGetString(Mathf.RoundToInt(value));
        foreach (var t in _texts) t.text = drawn;

        Log($"Cheque amount drawn: {drawn}");
    }

    private static void Log(string msg)
    {
        if (ChequeConfig.DebugLogging.Value) RepoCheque.Logger.LogInfo(msg);
    }
}
