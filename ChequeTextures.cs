using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RepoCheque;

/// <summary>
/// Loads cheque.png / cheque_back.png from the plugin folder, and draws a placeholder
/// cheque in code when the user hasn't supplied artwork yet.
/// </summary>
internal static class ChequeTextures
{
    internal const int PlaceholderWidth = 1024;
    internal const int PlaceholderHeight = 466; // 1024 / 2.2 -> the cheque aspect ratio

    private static Texture2D? _front;
    private static Texture2D? _back;
    private static bool _loaded;

    /// <summary>The cheque face. Never null once <see cref="Load"/> has run.</summary>
    internal static Texture2D Front { get { EnsureLoaded(); return _front!; } }

    /// <summary>The reverse face, or null when the user supplied no back image.</summary>
    internal static Texture2D? Back { get { EnsureLoaded(); return _back; } }

    private static void EnsureLoaded() { if (!_loaded) Load(); }

    internal static void Load()
    {
        _loaded = true;

        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";

        _back = TryLoad(dir, new[] { "cheque_back.png", "cheque-back.png" }, "back");
        _front = TryLoad(dir, new[] { "cheque.png", "cheque_front.png" }, "front", exclude: _back);

        if (_front == null)
        {
            RepoCheque.Logger.LogWarning(
                "No usable cheque front image found - using the built-in placeholder texture instead. " +
                $"Drop a PNG named 'cheque.png' (or anything with 'front' in the name) into: {dir}");
            _front = BuildPlaceholder();
        }

        if (_back == null)
            RepoCheque.Logger.LogInfo("No back image supplied - the reverse side will mirror the front.");
    }

    /// <summary>
    /// Looks for the exact filenames first, then falls back to any PNG in the folder whose
    /// name contains <paramref name="keyword"/>. That way artwork can keep whatever name the
    /// artist gave it, e.g. "Taxman's Cheque - Front.png".
    /// </summary>
    private static Texture2D? TryLoad(string dir, string[] exactNames, string keyword, Texture2D? exclude = null)
    {
        var candidates = new List<string>();

        foreach (string n in exactNames)
        {
            candidates.Add(Path.Combine(dir, n));
            candidates.Add(Path.Combine(dir, "RepoCheque", n));
        }

        foreach (string folder in new[] { dir, Path.Combine(dir, "RepoCheque") })
        {
            if (!Directory.Exists(folder)) continue;

            foreach (string p in Directory.GetFiles(folder, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(p);
                if (name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    candidates.Add(p);
            }
        }

        foreach (string path in candidates)
        {
            if (!File.Exists(path)) continue;
            if (exclude != null && exclude.name == Path.GetFileName(path)) continue;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
                if (!tex.LoadImage(bytes))
                {
                    RepoCheque.Logger.LogWarning($"'{path}' exists but is not a readable PNG/JPG. Ignoring it.");
                    Object.Destroy(tex);
                    continue;
                }

                string fileName = Path.GetFileName(path);
                tex.name = fileName;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.anisoLevel = 4;
                tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);

                RepoCheque.Logger.LogInfo($"Loaded {fileName} ({tex.width}x{tex.height}) from {path}");
                WarnIfOddAspect(fileName, tex);
                return tex;
            }
            catch (System.Exception e)
            {
                RepoCheque.Logger.LogWarning($"Failed to read '{path}': {e.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// True if the image has see-through pixels. Artwork with torn/ragged paper edges relies
    /// on this - the material then uses alpha cutout so the cheque takes the paper's silhouette
    /// instead of rendering a hard rectangle with black corners.
    /// </summary>
    internal static bool HasTransparency(Texture2D? tex)
    {
        if (tex == null) return false;

        try
        {
            // Sampling the mip chain's smallest level is enough to spot a cut-out border,
            // and avoids walking a million pixels.
            Color32[] px = tex.GetPixels32(Mathf.Max(tex.mipmapCount - 4, 0));
            foreach (Color32 p in px)
                if (p.a < 250) return true;
        }
        catch (System.Exception e)
        {
            RepoCheque.Logger.LogWarning($"Could not inspect '{tex.name}' for transparency: {e.Message}");
        }

        return false;
    }

    private static void WarnIfOddAspect(string fileName, Texture2D tex)
    {
        float aspect = (float)tex.width / tex.height;
        if (Mathf.Abs(aspect - 2.2f) > 0.35f)
        {
            RepoCheque.Logger.LogWarning(
                $"{fileName} is {tex.width}x{tex.height} (ratio {aspect:0.00}:1). The cheque mesh is " +
                $"2.2:1, so your image will look stretched. Ideal size is {PlaceholderWidth}x{PlaceholderHeight}.");
        }
    }

    /// <summary>
    /// Draws a plain but recognisable cheque so the mod is fully testable before any artwork exists.
    /// The amount box (right-hand side, just above the middle) is deliberately left blank -
    /// that is where the live money text gets drawn.
    /// </summary>
    private static Texture2D BuildPlaceholder()
    {
        const int w = PlaceholderWidth;
        const int h = PlaceholderHeight;

        var paper = new Color32(0xF2, 0xEE, 0xDE, 0xFF);
        var ink = new Color32(0x2B, 0x3A, 0x55, 0xFF);
        var faint = new Color32(0xC9, 0xD2, 0xE0, 0xFF);
        var accent = new Color32(0x7E, 0x9A, 0xB8, 0xFF);

        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = paper;

        // subtle guilloche-ish banding so it doesn't read as flat card stock
        for (int y = 0; y < h; y++)
        {
            if (y % 14 != 0) continue;
            for (int x = 0; x < w; x++)
            {
                float t = Mathf.Sin((x / (float)w) * Mathf.PI * 6f) * 0.5f + 0.5f;
                px[y * w + x] = Color32.Lerp(paper, faint, t * 0.35f);
            }
        }

        Rect(px, w, h, 0.02f, 0.04f, 0.98f, 0.96f, accent, thickness: 3);   // outer border
        Rect(px, w, h, 0.04f, 0.78f, 0.42f, 0.90f, ink, thickness: 0);       // bank name block
        Rect(px, w, h, 0.62f, 0.80f, 0.96f, 0.90f, faint, thickness: 2);     // date box

        Line(px, w, h, 0.04f, 0.455f, 0.58f, ink);                            // "pay to the order of"
        Line(px, w, h, 0.04f, 0.30f, 0.58f, faint);                           // amount-in-words line
        Line(px, w, h, 0.60f, 0.13f, 0.94f, ink);                             // signature line
        Line(px, w, h, 0.04f, 0.13f, 0.40f, faint);                           // memo line

        // The blank amount box: u 0.62 -> 0.96, v 0.54 -> 0.70.
        // Keep the inside clear; ChequeConfig.TextOffsetU/V default to its centre (0.79, 0.62).
        Rect(px, w, h, 0.62f, 0.54f, 0.96f, 0.70f, ink, thickness: 3);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true) { name = "cheque_placeholder" };
        tex.SetPixels32(px);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 4;
        tex.Apply(updateMipmaps: true);
        return tex;
    }

    /// <summary>Draws a rectangle in UV space. thickness 0 fills it solid.</summary>
    private static void Rect(Color32[] px, int w, int h,
                             float u0, float v0, float u1, float v1, Color32 c, int thickness)
    {
        int x0 = Mathf.Clamp((int)(u0 * w), 0, w - 1), x1 = Mathf.Clamp((int)(u1 * w), 0, w - 1);
        int y0 = Mathf.Clamp((int)(v0 * h), 0, h - 1), y1 = Mathf.Clamp((int)(v1 * h), 0, h - 1);

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                bool onEdge = x - x0 < thickness || x1 - x < thickness ||
                              y - y0 < thickness || y1 - y < thickness;
                if (thickness == 0 || onEdge) px[y * w + x] = c;
            }
    }

    /// <summary>Draws a horizontal rule in UV space.</summary>
    private static void Line(Color32[] px, int w, int h, float u0, float v, float u1, Color32 c)
    {
        int x0 = Mathf.Clamp((int)(u0 * w), 0, w - 1), x1 = Mathf.Clamp((int)(u1 * w), 0, w - 1);
        int y = Mathf.Clamp((int)(v * h), 0, h - 1);

        for (int x = x0; x <= x1; x++)
            for (int dy = 0; dy < 3; dy++)
                if (y + dy < h) px[(y + dy) * w + x] = c;
    }
}
