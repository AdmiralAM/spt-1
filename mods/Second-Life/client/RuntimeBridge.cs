using System;
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
        string pendingRaidId;
        long raidSequence;
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
                    () => minimumPlayerDistance?.Value ?? 75f,
                    logInfo);
                harmony = new Harmony(HarmonyId);
                active = this;
                harmony.Patch(
                    contract.CreateCorpse,
                    postfix: new HarmonyMethod(typeof(RuntimeBridge), nameof(CorpseCreatedPostfix)));
                harmony.Patch(
                    contract.InitiateGameStopping,
                    prefix: new HarmonyMethod(typeof(RuntimeBridge), nameof(GameStoppingPrefix)));
                harmony.Patch(
                    contract.GamePlayerOwnerCleanup,
                    prefix: new HarmonyMethod(typeof(RuntimeBridge), nameof(OwnerCleanupPrefix)),
                    postfix: new HarmonyMethod(typeof(RuntimeBridge), nameof(OwnerCleanupPostfix)));
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
            if (active == null) return true;
            try { return active.ContinueNativeFinalization(__instance); }
            catch (Exception exception)
            {
                active.finalizationGate?.AbortPendingRecovery();
                active.logWarning?.Invoke("Recovery death-boundary failed open; continuing native death: " + (exception.InnerException?.Message ?? exception.Message));
                return true;
            }
        }

        static void OwnerCleanupPrefix(object __instance, out object __state)
        {
            __state = null;
            RecoveryRuntimeContract contract = active?.runtimeContract;
            if (contract == null || __instance == null) return;
            object currentPlayer = contract.GamePlayerOwnerMyPlayer.GetValue(null);
            object cleanupOwnerPlayer = contract.GamePlayerOwnerPlayer.GetValue(__instance, null);
            if (OwnerHandoffGuard.MustPreserveReplacement(currentPlayer, cleanupOwnerPlayer))
                __state = currentPlayer;
        }

        static void OwnerCleanupPostfix(object __state)
        {
            RecoveryRuntimeContract contract = active?.runtimeContract;
            if (contract == null || __state == null) return;
            if (contract.GamePlayerOwnerMyPlayer.GetValue(null) == null)
                contract.GamePlayerOwnerMyPlayer.SetValue(null, __state);
        }

        void CaptureCorpse(object player, object corpse)
        {
            if (enabled == null || !enabled.Value || player == null || corpse == null) return;
            if (!ReadBoolean(player, "IsYourPlayer")) return;

            pendingCorpse = corpse;
            pendingCorpseEquipment = ReadObject(corpse, "Item");
            pendingCorpseEquipmentRootId = ReadString(pendingCorpseEquipment, "Id");
            pendingProfileId = ReadString(player, "ProfileId");
            RecoveryState priorState = finalizationGate.Snapshot.State;
            if (string.IsNullOrWhiteSpace(pendingRaidId) ||
                priorState == RecoveryState.Disabled ||
                priorState == RecoveryState.FinalDeath ||
                priorState == RecoveryState.Extracted)
            {
                raidSequence++;
                pendingRaidId = pendingProfileId + ":raid-" + raidSequence;
                warnedExecutorUnavailable = false;
            }
            logInfo?.Invoke("Recovery trace: captured local-player corpse for " + pendingRaidId + "; prior-state=" + priorState + ".");
            if (string.IsNullOrWhiteSpace(pendingCorpseEquipmentRootId))
                logWarning?.Invoke("Native corpse has no stable equipment-root ID; recovery will fail closed.");
        }

        bool ContinueNativeFinalization(object localGame)
        {
            if (nativeFinalizationReentry) return true;
            if (enabled == null || !enabled.Value) return true;

            string raidId = pendingRaidId;
            if (string.IsNullOrWhiteSpace(raidId))
            {
                logWarning?.Invoke("Recovery has no captured raid identity; continuing native finalization.");
                return true;
            }
            RecoveryState state = finalizationGate.Snapshot.State;
            RecoveryExecutionPlan plan = null;
            string failure = null;
            bool executorReady = state == RecoveryState.RecoveryPending;
            if (!executorReady && state != RecoveryState.RecoverySpawned && executor != null)
            {
                try
                {
                    executorReady = executor.TryPrepare(localGame, pendingCorpseEquipment, pendingCorpse, pendingProfileId, out plan, out failure);
                }
                catch (Exception exception)
                {
                    failure = "recovery preparation threw: " + (exception.InnerException?.Message ?? exception.Message);
                    executorReady = false;
                }
            }
            NativeFinalizationDecision decision = finalizationGate.HandleDeathBoundary(
                raidId,
                pendingCorpseEquipmentRootId,
                executorReady);

            if (decision == NativeFinalizationDecision.SuppressDuplicate)
                return false;
            if (decision == NativeFinalizationDecision.SuppressForRecovery && plan != null)
            {
                logInfo?.Invoke("Paid-healing inventory scan: " + plan.PaidHealingScanSummary);
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
            await WaitForNeutralInput();
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
                        BeginRecoveryAfterConfirmationDelay(localGame, plan);
                        return;
                    }
                    CancelPlanSafely(plan);
                    finalizationGate.AbortPendingRecovery();
                    logInfo?.Invoke("Paid recovery unavailable because stash rubles are insufficient; continuing native death.");
                    ResumeNativeFinalization(localGame);
                };
                Action cancel = () =>
                {
                    if (resolved) return;
                    resolved = true;
                    CancelPlanSafely(plan);
                    finalizationGate.AbortPendingRecovery();
                    logInfo?.Invoke("Paid healing declined; continuing native death.");
                    ResumeNativeFinalization(localGame);
                };
                if (!RuntimePaidHealing.TryShowNativeConfirmation(plan.PaidHealingCost, plan.CanAffordPaidHealing, accept, cancel, out string promptFailure))
                {
                    resolved = true;
                    CancelPlanSafely(plan);
                    finalizationGate.AbortPendingRecovery();
                    logWarning?.Invoke("Paid-healing offer failed closed; resuming native death: " + promptFailure);
                    ResumeNativeFinalization(localGame);
                }
            }
            catch (Exception exception)
            {
                CancelPlanSafely(plan);
                finalizationGate.AbortPendingRecovery();
                logWarning?.Invoke("Paid-healing offer failed closed; resuming native death: " + exception.Message);
                ResumeNativeFinalization(localGame);
            }
        }

        async void BeginRecoveryAfterConfirmationDelay(object localGame, RecoveryExecutionPlan plan)
        {
            // Keep the confirmation legible and absorb the key/button release so
            // recovery never fires on the same frame as the user's click.
            await Task.Delay(650);
            await WaitForNeutralInput();
            if (finalizationGate.Snapshot.State == RecoveryState.RecoveryPending)
                BeginRecovery(localGame, plan);
        }

        void CancelPlanSafely(RecoveryExecutionPlan plan)
        {
            try { plan?.CancelPaidHealing(); }
            catch (Exception exception)
            {
                logWarning?.Invoke("Recovery cancellation cleanup failed; native death will continue: " + (exception.InnerException?.Message ?? exception.Message));
            }
        }

        static async Task WaitForNeutralInput()
        {
            Type input = Type.GetType("UnityEngine.Input, UnityEngine.InputLegacyModule", throwOnError: false);
            var anyKey = input?.GetProperty("anyKey", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (anyKey == null)
            {
                await Task.Delay(350);
                return;
            }

            int neutralSamples = 0;
            while (neutralSamples < 5)
            {
                bool pressed = anyKey.GetValue(null, null) is bool value && value;
                neutralSamples = pressed ? 0 : neutralSamples + 1;
                await Task.Delay(50);
            }
        }

        void BeginRecovery(object localGame, RecoveryExecutionPlan plan)
        {
            executor.Execute(
                plan,
                recoveryRootId => finalizationGate.ConfirmRecovery(recoveryRootId),
                recoveryRootId =>
                {
                    logInfo?.Invoke("One-time recovery spawned after paid healing (" + plan.PaidHealingCost + " rubles), equipment root " + recoveryRootId + ", emergency armament=" + (plan.HasEmergencyArmament ? "owned pistol plus spare magazine" : "unarmed; no complete stash set") + ".");
                },
                exception =>
                {
                    finalizationGate.AbortPendingRecovery();
                    logWarning?.Invoke("Recovery failed; cleanup attempted; resuming native death: " + exception);
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
            pendingRaidId = null;
            raidSequence = 0;
        }
    }
}
