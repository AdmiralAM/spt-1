using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BepInEx.Configuration;
using HarmonyLib;

namespace Admiral.SecondLife.Client
{
    internal sealed class RuntimeBridge : IDisposable
    {
        const string HarmonyId = "com.admiralam.secondlife.runtime";
        static RuntimeBridge active;

        readonly ConfigEntry<bool> enabled;
        readonly ConfigEntry<string> eligiblePistolTemplates;
        readonly ConfigEntry<float> recoveryDelaySeconds;
        readonly ConfigEntry<float> minimumCorpseDistance;
        readonly ConfigEntry<float> minimumPlayerDistance;
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        readonly RecoveryFinalizationGate finalizationGate = new RecoveryFinalizationGate();
        Harmony harmony;
        RecoveryRuntimeContract runtimeContract;
        RecoveryExecutor executor;
        object pendingCorpseEquipment;
        object pendingCorpse;
        string pendingCorpseEquipmentRootId;
        string pendingProfileId;
        bool warnedExecutorUnavailable;
        bool nativeFinalizationReentry;

        internal RuntimeBridge(
            ConfigEntry<bool> enabled,
            ConfigEntry<string> eligiblePistolTemplates,
            ConfigEntry<float> recoveryDelaySeconds,
            ConfigEntry<float> minimumCorpseDistance,
            ConfigEntry<float> minimumPlayerDistance,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            this.enabled = enabled;
            this.eligiblePistolTemplates = eligiblePistolTemplates;
            this.recoveryDelaySeconds = recoveryDelaySeconds;
            this.minimumCorpseDistance = minimumCorpseDistance;
            this.minimumPlayerDistance = minimumPlayerDistance;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                if (!RecoveryRuntimeContract.TryResolve(out RecoveryRuntimeContract contract, out string failure))
                    return Fail("SPT 4.1 recovery contract rejected: " + failure + "; module remains inert.");

                runtimeContract = contract;
                executor = new RecoveryExecutor(
                    contract,
                    () => eligiblePistolTemplates?.Value,
                    () => minimumCorpseDistance?.Value ?? 100f,
                    () => minimumPlayerDistance?.Value ?? 75f);
                harmony = new Harmony(HarmonyId);
                active = this;
                harmony.Patch(
                    contract.CreateCorpse,
                    postfix: new HarmonyMethod(typeof(RuntimeBridge), nameof(CorpseCreatedPostfix)));
                harmony.Patch(
                    contract.InitiateGameStopping,
                    prefix: new HarmonyMethod(typeof(RuntimeBridge), nameof(GameStoppingPrefix)));
                logInfo?.Invoke("Verified SPT 4.1 recovery ownership/construction contract and patched the death boundary.");
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

            pendingCorpse = corpse;
            pendingCorpseEquipment = ReadObject(corpse, "Item");
            pendingCorpseEquipmentRootId = ReadString(pendingCorpseEquipment, "Id");
            pendingProfileId = ReadString(player, "ProfileId");
            if (string.IsNullOrWhiteSpace(pendingCorpseEquipmentRootId))
                logWarning?.Invoke("Native corpse has no stable equipment-root ID; recovery will fail closed.");
        }

        bool ContinueNativeFinalization(object localGame)
        {
            if (nativeFinalizationReentry) return true;
            if (enabled == null || !enabled.Value) return true;

            string raidId = pendingProfileId + ":" + RuntimeHelpers.GetHashCode(localGame).ToString("X8");
            RecoveryState state = finalizationGate.Snapshot.State;
            RecoveryExecutionPlan plan = null;
            string failure = null;
            bool executorReady = state == RecoveryState.RecoveryPending ||
                (state != RecoveryState.RecoverySpawned &&
                 executor != null &&
                 executor.TryPrepare(localGame, pendingCorpseEquipment, pendingCorpse, pendingProfileId, out plan, out failure));
            NativeFinalizationDecision decision = finalizationGate.HandleDeathBoundary(
                raidId,
                pendingCorpseEquipmentRootId,
                executorReady);

            if (decision == NativeFinalizationDecision.SuppressDuplicate)
                return false;
            if (decision == NativeFinalizationDecision.SuppressForRecovery && plan != null)
            {
                OfferRecoveryAfterDelay(localGame, plan);
                return false;
            }

            if (decision != NativeFinalizationDecision.ContinueNative) return false;
            if (!string.IsNullOrWhiteSpace(failure) && !warnedExecutorUnavailable)
            {
                warnedExecutorUnavailable = true;
                logWarning?.Invoke("Recovery preflight rejected; continuing native death: " + failure);
            }
            return true;
        }

        async void OfferRecoveryAfterDelay(object localGame, RecoveryExecutionPlan plan)
        {
            float seconds = Math.Max(0f, Math.Min(60f, recoveryDelaySeconds?.Value ?? 0f));
            if (seconds > 0f) await Task.Delay(TimeSpan.FromSeconds(seconds));
            if (finalizationGate.Snapshot.State != RecoveryState.RecoveryPending) return;

            try
            {
                bool resolved = false;
                Action accept = () =>
                {
                    if (resolved) return;
                    resolved = true;
                    if (plan.CanAffordPaidHealing)
                    {
                        BeginRecovery(localGame, plan);
                        return;
                    }
                    finalizationGate.AbortPendingRecovery();
                    logInfo?.Invoke("Paid recovery unavailable because stash rubles are insufficient; continuing native death.");
                    ResumeNativeFinalization(localGame);
                };
                Action cancel = () =>
                {
                    if (resolved) return;
                    resolved = true;
                    finalizationGate.AbortPendingRecovery();
                    logInfo?.Invoke("Paid healing declined; continuing native death.");
                    ResumeNativeFinalization(localGame);
                };
                if (!RuntimePaidHealing.TryShowNativeConfirmation(plan.PaidHealingCost, plan.CanAffordPaidHealing, accept, cancel, out string promptFailure))
                {
                    resolved = true;
                    finalizationGate.AbortPendingRecovery();
                    logWarning?.Invoke("Paid-healing offer failed closed; resuming native death: " + promptFailure);
                    ResumeNativeFinalization(localGame);
                }
            }
            catch (Exception exception)
            {
                finalizationGate.AbortPendingRecovery();
                logWarning?.Invoke("Paid-healing offer failed closed; resuming native death: " + exception.Message);
                ResumeNativeFinalization(localGame);
            }
        }

        void BeginRecovery(object localGame, RecoveryExecutionPlan plan)
        {
            executor.Execute(
                plan,
                recoveryRootId =>
                {
                    if (finalizationGate.ConfirmRecovery(recoveryRootId))
                    {
                        logInfo?.Invoke("One-time recovery spawned after paid healing (" + plan.PaidHealingCost + " rubles), equipment root " + recoveryRootId + ".");
                        return;
                    }
                    logWarning?.Invoke("Recovery spawned but lifecycle confirmation failed; native finalization resumed.");
                    ResumeNativeFinalization(localGame);
                },
                exception =>
                {
                    finalizationGate.AbortPendingRecovery();
                    logWarning?.Invoke("Recovery failed safely; healing debit rolled back; resuming native death: " + exception.Message);
                    ResumeNativeFinalization(localGame);
                });
        }

        void ResumeNativeFinalization(object localGame)
        {
            try
            {
                nativeFinalizationReentry = true;
                runtimeContract.InitiateGameStopping.Invoke(localGame, null);
            }
            catch (Exception exception)
            {
                logWarning?.Invoke("Native death re-entry failed: " + (exception.InnerException?.Message ?? exception.Message));
            }
            finally
            {
                nativeFinalizationReentry = false;
            }
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

        static object ReadObject(object instance, string propertyName) =>
            instance == null ? null : AccessTools.Property(instance.GetType(), propertyName)?.GetValue(instance, null);

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
            executor = null;
            runtimeContract = null;
            pendingCorpseEquipment = null;
            pendingCorpse = null;
            pendingCorpseEquipmentRootId = null;
            pendingProfileId = null;
        }
    }
}
