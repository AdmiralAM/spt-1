using System;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace AdmiralCompatibilitySuite.Healing;

[HarmonyPatch(typeof(Player.MedsController), nameof(Player.MedsController.Drop))]
internal static class MedsDropPatch
{
    private static Player.MedsController? pending;
    private static float deadline;

    internal static bool TryBegin(Player.MedsController controller)
    {
        // A stale request must never affect a later use of the same med item.
        if (ReferenceEquals(pending, controller) && Time.realtimeSinceStartup <= deadline)
            return false;
        pending = controller;
        deadline = Time.realtimeSinceStartup + 0.5f;
        return true;
    }

    internal static void Cancel(Player.MedsController controller)
    {
        if (ReferenceEquals(pending, controller)) pending = null;
    }

    internal static void Clear() => pending = null;

    private static bool Prefix(Player.MedsController __instance, Action callback)
    {
        if (!ReferenceEquals(pending, __instance))
            return true;
        pending = null;
        if (Time.realtimeSinceStartup > deadline || __instance.CurrentOperation is null)
            return true;

        try
        {
            // Same vanilla callback and state transition as the regular outro,
            // without its 600 ms wait. No inventory event is cleared or forced.
            __instance.Destroyed = true;
            __instance.CurrentOperation.HideWeapon(callback);
            __instance.FastForwardCurrentState();
            return false;
        }
        catch (Exception)
        {
            // Leave EFT's original Drop path available on any unexpected state.
            __instance.Destroyed = false;
            return true;
        }
    }
}
