using BepInEx;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace AdmiralCompatibilitySuite.Healing;

[BepInPlugin("com.admiralam.compatibility-suite.healing-interrupt", "Admiral Healing Interrupt", "0.1.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private Harmony? harmony;

    private void Awake()
    {
        harmony = new Harmony("com.admiralam.compatibility-suite.healing-interrupt");
        harmony.PatchAll(typeof(Plugin).Assembly);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.End))
            return;

        Player? player = Singleton<GameWorld>.Instance?.MainPlayer;
        if (player?.HandsController is not Player.MedsController meds
            || meds.Destroyed
            || meds.CurrentHandsOperation is null
            || meds.CurrentOperation is null
            || meds.Item?.Owner is null)
        {
            return;
        }

        if (!MedsDropPatch.TryBegin(meds))
            return;

        try
        {
            // EFT's own cancellation stops the medical effect. The normal
            // weapon-switch path remains responsible for restoring the weapon.
            meds.CurrentOperation.Remove();
            player.TrySetLastEquippedWeapon(false, null);
        }
        catch (System.Exception error)
        {
            MedsDropPatch.Cancel(meds);
            Logger.LogWarning($"Healing interrupt could not start safely: {error}");
        }
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        MedsDropPatch.Clear();
    }
}
