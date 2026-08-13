using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RepoCheque;

// GUID is also the config filename -> BepInEx\config\RepoCheque.cfg
[BepInPlugin("RepoCheque", "RepoCheque", "1.0.0")]
// REPOLib's real GUID is "REPOLib" (verified by decompiling REPOLib.dll v4.2.0),
// NOT "Zehs.REPOLib". Soft, not hard: this mod only patches vanilla classes and
// never calls REPOLib's API, so we want correct load order without a hard failure
// if REPOLib is ever absent or renamed.
[BepInDependency("REPOLib", BepInDependency.DependencyFlags.SoftDependency)]
public class RepoCheque : BaseUnityPlugin
{
    internal static RepoCheque Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    /// <summary>False when a patch failed to apply, e.g. after a game update renamed something.</summary>
    internal static bool Healthy { get; private set; }

    private void Awake()
    {
        Instance = this;

        // Prevent the plugin from being deleted
        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        ChequeConfig.Init(Config);

        if (!ChequeConfig.Enabled.Value)
        {
            Logger.LogWarning("RepoCheque is disabled in the config - the vanilla money bag will be used.");
            return;
        }

        try
        {
            ChequeTextures.Load();
        }
        catch (System.Exception e)
        {
            // Bad artwork must never stop the mod: the loader falls back to a drawn placeholder.
            Logger.LogError($"Loading the cheque artwork failed; continuing with the placeholder.\n{e}");
        }

        Patch();

        if (Healthy)
            Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded! " +
                           "Built against game build 23363152.");
        else
            Logger.LogWarning($"{Info.Metadata.GUID} v{Info.Metadata.Version} loaded but is INACTIVE " +
                              "- see the error above. The vanilla money bag will be used.");
    }

    internal void Patch()
    {
        try
        {
            Harmony ??= new Harmony(Info.Metadata.GUID);
            Harmony.PatchAll();
            Healthy = true;
        }
        catch (System.Exception e)
        {
            Healthy = false;
            Logger.LogError(
                "RepoCheque could not apply its patches and has disabled itself. Your game is unaffected " +
                "and the vanilla money bag will be used. This usually means R.E.P.O. was updated and a " +
                "class or method was renamed.\n" + e);
            Unpatch();
        }
    }

    internal void Unpatch()
    {
        try { Harmony?.UnpatchSelf(); }
        catch (System.Exception e) { Logger.LogWarning($"Unpatch failed: {e.Message}"); }
    }
}
