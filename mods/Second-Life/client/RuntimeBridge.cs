using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;

namespace Admiral.SecondLife.Client
{
    internal sealed class RuntimeBridge : IDisposable
    {
        const string HarmonyId = "com.admiralam.secondlife.runtime";
        static RuntimeBridge active;

        readonly ConfigEntry<bool> enabled;
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        readonly RecoveryStateMachine lifecycle = new RecoveryStateMachine();
        Harmony harmony;
        string pendingCorpseId;
        string pendingProfileId;
        bool warnedExecutorUnavailable;

        internal RuntimeBridge(
            ConfigEntry<bool> enabled,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.enabled = enabled;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                Type playerType = AccessTools.TypeByName("EFT.Player");
                Type localGameType = AccessTools.TypeByName("EFT.LocalGame");
                if (playerType == null || localGameType == null)
                    return Fail("SPT 4.1 Player/LocalGame types were not found; module remains inert.");

                MethodInfo createCorpse = playerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .SingleOrDefault(method => method.Name == "CreateCorpse" && !method.IsGenericMethod && method.GetParameters().Length == 0);
                MethodInfo initiateGameStopping = localGameType.BaseType?.GetMethod(
                    "InitiateGameStopping",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);

                if (createCorpse == null || initiateGameStopping == null)
                    return Fail("Exact SPT 4.1 death boundary changed; module remains inert.");

                harmony = new Harmony(HarmonyId);
                active = this;
                harmony.Patch(
                    createCorpse,
                    postfix: new HarmonyMethod(typeof(RuntimeBridge), nameof(CorpseCreatedPostfix)));
                harmony.Patch(
                    initiateGameStopping,
                    prefix: new HarmonyMethod(typeof(RuntimeBridge), nameof(GameStoppingPrefix)));
                logInfo?.Invoke("Verified and patched the SPT 4.1 corpse/finalization boundary.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Death-boundary installation failed safely: " + exception.Message);
            }
        }

        static void CorpseCreatedPostfix(object __instance, object __result)
        {
            active?.CaptureCorpse(__instance, __result);
        }

        static bool GameStoppingPrefix(object __instance)
        {
            return active == null || active.ContinueNativeFinalization(__instance);
        }

        void CaptureCorpse(object player, object corpse)
        {
            if (enabled == null || !enabled.Value || player == null || corpse == null) return;
            if (!ReadBoolean(player, "IsYourPlayer")) return;

            pendingCorpseId = ReadString(corpse, "Id");
            pendingProfileId = ReadString(player, "ProfileId");
            if (string.IsNullOrWhiteSpace(pendingCorpseId))
                logWarning?.Invoke("Native corpse has no stable ID; recovery will fail closed.");
        }

        bool ContinueNativeFinalization(object localGame)
        {
            if (enabled == null || !enabled.Value) return true;

            string raidId = pendingProfileId + ":" + RuntimeHelpers.GetHashCode(localGame).ToString("X8");
            if (!lifecycle.TryApply(RecoveryTransition.EnableRaid, raidId: raidId) &&
                lifecycle.Snapshot.RaidId != raidId)
                return true;

            if (!lifecycle.TryApply(RecoveryTransition.CaptureFirstDeath, originalCorpseId: pendingCorpseId))
                return true;

            // The boundary is executable, but native finalization is never suppressed
            // until the player reconstruction executor proves all prerequisites.
            lifecycle.TryApply(RecoveryTransition.AbortToNativeDeath);
            if (!warnedExecutorUnavailable)
            {
                warnedExecutorUnavailable = true;
                logWarning?.Invoke("Recovery executor is not ready; continuing the native death path.");
            }
            return true;
        }

        static bool ReadBoolean(object instance, string propertyName)
        {
            object value = AccessTools.Property(instance.GetType(), propertyName)?.GetValue(instance, null);
            return value is bool result && result;
        }

        static string ReadString(object instance, string propertyName)
        {
            object value = AccessTools.Property(instance.GetType(), propertyName)?.GetValue(instance, null);
            return value?.ToString();
        }

        bool Fail(string message)
        {
            logWarning?.Invoke(message);
            return false;
        }

        public void Dispose()
        {
            try { harmony?.UnpatchSelf(); }
            catch { }
            if (ReferenceEquals(active, this)) active = null;
            harmony = null;
            pendingCorpseId = null;
            pendingProfileId = null;
        }
    }
}
