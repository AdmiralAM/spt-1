using System;
using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Admiral.SecondLife.Client
{
    internal sealed class RecoveryExecutionPlan
    {
        readonly RecoveryRuntimeContract contract;
        readonly object localGame;
        readonly object originalPlayer;
        readonly object originalOwner;
        readonly IDictionary players;
        readonly Delegate playerFactory;
        readonly Delegate ownerFactory;
        readonly RecoveryInventoryLease inventoryLease;
        readonly RuntimeSpawnSelection spawnSelection;
        readonly RuntimeArmament armament;
        readonly RuntimeServerArmamentReservation armamentReservation;
        readonly RuntimePaidHealing paidHealing;
        readonly Action<string> trace;

        internal RecoveryExecutionPlan(
            RecoveryRuntimeContract contract,
            object localGame,
            object originalPlayer,
            object originalOwner,
            IDictionary players,
            Delegate playerFactory,
            Delegate ownerFactory,
            RecoveryInventoryLease inventoryLease,
            RuntimeSpawnSelection spawnSelection,
            RuntimeArmament armament,
            RuntimeServerArmamentReservation armamentReservation,
            RuntimePaidHealing paidHealing,
            string profileId,
            Action<string> trace)
        {
            this.contract = contract;
            this.localGame = localGame;
            this.originalPlayer = originalPlayer;
            this.originalOwner = originalOwner;
            this.players = players;
            this.playerFactory = playerFactory;
            this.ownerFactory = ownerFactory;
            this.inventoryLease = inventoryLease;
            this.spawnSelection = spawnSelection;
            this.armament = armament;
            this.armamentReservation = armamentReservation;
            this.paidHealing = paidHealing;
            this.trace = trace;
            ProfileId = profileId;
        }

        internal string ProfileId { get; }
        internal string RecoveryEquipmentRootId => inventoryLease.RecoveryEquipmentRootId;
        internal bool HasEmergencyArmament => armament != null;
        internal int PaidHealingCost => paidHealing.Cost;
        internal bool CanAffordPaidHealing => paidHealing.CanAfford;
        internal string PaidHealingScanSummary => paidHealing.ScanSummary;
        internal void CancelPaidHealing()
        {
            Exception failure = null;
            try { paidHealing.Cancel(); }
            catch (Exception exception) { failure = exception; }
            try { armamentReservation?.Refund(); }
            catch (Exception exception)
            {
                failure = failure == null
                    ? exception
                    : new InvalidOperationException(failure.Message + "; armament refund failed: " + (exception.InnerException?.Message ?? exception.Message), failure);
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        internal async Task ExecuteAsync(Func<string, bool> confirmRecovery)
        {
            if (!inventoryLease.Apply()) throw new InvalidOperationException("profile inventory ownership changed before recovery");

            object newPlayer = null;
            object newOwner = null;
            bool attached = false;
            bool newPlayerRegistered = false;
            bool newCameraCreated = false;
            bool playerDictionaryReplaced = false;
            object playerDictionaryKey = null;
            object gameWorld = ReadProperty(originalPlayer, "GameWorld");
            object retiredCullingSampler = null;
            if (gameWorld == null) throw new InvalidOperationException("active GameWorld is unavailable");
            Exception failure = null;
            string stage = "inventory-apply";
            try
            {
                Trace(stage, gameWorld, newPlayer);
                trace?.Invoke("Recovery trace: protected transfers=" + inventoryLease.ProtectedTransferSummary);
                playerDictionaryKey = ResolvePlayerDictionaryKey();
                stage = "recovery-assets-load";
                await LoadRecoveryAssets();
                Trace(stage, gameWorld, newPlayer);
                stage = "old-player-unregister";
                contract.UnregisterWorldPlayer.Invoke(gameWorld, new[] { originalPlayer });
                Trace(stage, gameWorld, newPlayer);
                stage = "old-camera-destroy";
                contract.DestroyPlayerCamera.Invoke(null, new[] { originalPlayer });
                await WaitForCameraRemoval(originalPlayer);
                retiredCullingSampler = await RemoveCurrentCullingSampler();
                ClearCullingCameraState();
                Trace(stage, gameWorld, newPlayer);
                stage = "new-player-create";
                var creationTask = playerFactory.DynamicInvoke() as Task;
                if (creationTask == null) throw new InvalidOperationException("player factory did not return a Task");
                await creationTask;
                newPlayer = creationTask.GetType().GetProperty("Result")?.GetValue(creationTask, null);
                if (newPlayer == null) throw new InvalidOperationException("player factory returned no LocalPlayer");
                // Player.Init registers the player before the factory task completes.
                // Track that ownership immediately so every later failure unregisters it.
                newPlayerRegistered = true;
                Trace(stage, gameWorld, newPlayer);
                stage = "new-player-prepare";
                RuntimeSafeSpawnSelector.Apply(newPlayer, spawnSelection);
                RuntimeArmamentService.ValidatePreloaded(inventoryLease.RecoveryEquipment, armament);
                stage = "new-owner-create";
                newOwner = ownerFactory.DynamicInvoke(newPlayer);
                if (newOwner == null) throw new InvalidOperationException("owner factory returned no PlayerOwner");

                contract.LocalPlayer.SetValue(localGame, newPlayer);
                contract.PlayerOwner.SetValue(localGame, newOwner);
                players[playerDictionaryKey] = newPlayer;
                playerDictionaryReplaced = true;
                Trace(stage, gameWorld, newPlayer);
                // Player.Init, reached by the captured native factory, already calls
                // GameWorld.RegisterPlayer. Registering it a second time corrupts the
                // RegisteredPlayers list and leaves world/camera consumers ambiguous.
                stage = "new-camera-create";
                object newCameraController = contract.CreatePlayerCamera.Invoke(null, new[] { newPlayer });
                newCameraCreated = true;
                RegisterNewCullingCamera(newCameraController);
                Trace(stage, gameWorld, newPlayer);
                stage = "new-player-spawn";
                contract.Spawn.Invoke(localGame, null);
                stage = "new-culling-ready";
                await WaitForReplacementCullingSampler(retiredCullingSampler);
                paidHealing.Apply(newPlayer);
                ValidateAttachment(gameWorld, newPlayer);
                Trace(stage, gameWorld, newPlayer);
                stage = "server-settlement";
                armamentReservation?.Commit();
                paidHealing.FinalizeDebit(newPlayer);
                armamentReservation?.FinalizeReservation();
                if (confirmRecovery == null || !confirmRecovery(RecoveryEquipmentRootId))
                    throw new InvalidOperationException("recovery lifecycle confirmation was rejected");
                // CleanupOnDestroy always clears GamePlayerOwner._myPlayer. Rebind
                // the replacement immediately after retiring the old owner.
                TryCleanupOwner(originalOwner);
                ValidateAttachment(gameWorld, newPlayer);
                RefreshDynamicMapsPlayerMarker();
                Trace(stage, gameWorld, newPlayer);
                attached = true;
                paidHealing.ReleaseDebit();
                armamentReservation?.ReleaseReservation();
                // EFT adds Corpse to the original player's own GameObject. Do not
                // deactivate that object after handoff or the corpse disappears
                // with it. Player.Dispose preserves PlayerBody when Corpse exists.
                try { DisposeStrict(originalPlayer); }
                catch (Exception exception) { trace?.Invoke("Recovery trace: original-player disposal warning: " + Unwrap(exception).Message); }
            }
            catch (Exception exception)
            {
                Exception root = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;
                failure = new InvalidOperationException("recovery stage " + stage + " failed: " + root.Message + "; " + DescribeState(gameWorld, newPlayer), root);
            }

            if (!attached)
            {
                var cleanupFailures = new System.Collections.Generic.List<string>();
                TryCleanupStep(() => paidHealing.Rollback(), "payment refund", cleanupFailures);
                TryCleanupStep(() => armamentReservation?.Refund(), "armament refund", cleanupFailures);
                TryCleanupStep(() => TryDeactivate(newPlayer), "new player deactivate", cleanupFailures);
                if (newCameraCreated)
                {
                    TryCleanupStep(() => contract.DestroyPlayerCamera.Invoke(null, new[] { newPlayer }), "new camera destroy", cleanupFailures);
                    try { await WaitForCameraRemoval(newPlayer); }
                    catch (Exception exception) { cleanupFailures.Add("new camera removal: " + exception.Message); }
                    TryCleanupStep(ClearCullingCameraState, "new culling camera clear", cleanupFailures);
                }
                if (newPlayerRegistered) TryCleanupStep(() => contract.UnregisterWorldPlayer.Invoke(gameWorld, new[] { newPlayer }), "new player unregister", cleanupFailures);
                TryCleanupStep(() => TryCleanupOwner(newOwner), "new owner cleanup", cleanupFailures);
                TryCleanupStep(() => DisposeStrict(newPlayer), "new player dispose", cleanupFailures);
                TryCleanupStep(() => contract.LocalPlayer.SetValue(localGame, originalPlayer), "local player restore", cleanupFailures);
                TryCleanupStep(() => contract.PlayerOwner.SetValue(localGame, originalOwner), "player owner restore", cleanupFailures);
                TryCleanupStep(() => contract.GamePlayerOwnerMyPlayer.SetValue(null, originalPlayer), "global player restore", cleanupFailures);
                if (playerDictionaryReplaced)
                    TryCleanupStep(() => players[playerDictionaryKey] = originalPlayer, "player dictionary restore", cleanupFailures);
                TryCleanupStep(() =>
                {
                    if (!inventoryLease.Rollback()) throw new InvalidOperationException("profile inventory rollback was rejected");
                }, "inventory restore", cleanupFailures);

                if (cleanupFailures.Count > 0)
                {
                    string cleanupMessage = string.Join("; ", cleanupFailures);
                    failure = failure == null
                        ? new InvalidOperationException("recovery cleanup failed: " + cleanupMessage)
                        : new InvalidOperationException(failure.Message + "; cleanup failures: " + cleanupMessage, failure);
                }
            }

            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        async Task LoadRecoveryAssets()
        {
            object taskObject = contract.LoadItemBundles.Invoke(null, new[] { inventoryLease.RecoveryEquipment });
            if (!(taskObject is Task task)) throw new InvalidOperationException("native recovery asset loader returned no Task");
            await task;
        }

        void Trace(string stage, object gameWorld, object newPlayer)
        {
            trace?.Invoke("Recovery trace: " + stage + "; " + DescribeState(gameWorld, newPlayer));
        }

        string DescribeState(object gameWorld, object newPlayer)
        {
            try
            {
                object localPlayer = contract.LocalPlayer.GetValue(localGame);
                object ownerPlayer = contract.GamePlayerOwnerMyPlayer.GetValue(null);
                int registrations = 0;
                if (newPlayer != null && ReadField(gameWorld, "RegisteredPlayers") is IEnumerable registered)
                    foreach (object player in registered) if (ReferenceEquals(player, newPlayer)) registrations++;
                return "local=" + ReferenceName(localPlayer, originalPlayer, newPlayer) +
                    ", global=" + ReferenceName(ownerPlayer, originalPlayer, newPlayer) +
                    ", main=" + ReferenceName(ReadField(gameWorld, "MainPlayer"), originalPlayer, newPlayer) +
                    ", newRegistrations=" + registrations +
                    ", sampler=" + (contract.CullingSamplerInstance.GetValue(null, null) == null ? "none" : "live");
            }
            catch (Exception exception)
            {
                return "state-unavailable=" + exception.Message;
            }
        }

        static string ReferenceName(object value, object originalPlayer, object newPlayer)
        {
            if (value == null) return "null";
            if (ReferenceEquals(value, originalPlayer)) return "original";
            if (ReferenceEquals(value, newPlayer)) return "replacement";
            return "other";
        }

        static void TryCleanupStep(Action action, string name, System.Collections.Generic.List<string> failures)
        {
            try { action(); }
            catch (Exception exception) { failures.Add(name + ": " + (exception.InnerException?.Message ?? exception.Message)); }
        }

        async Task WaitForCameraRemoval(object player)
        {
            object gameObject = ReadProperty(player, "gameObject");
            Type cameraType = contract.DestroyPlayerCamera.DeclaringType;
            MethodInfo getComponent = gameObject?.GetType().GetMethod("GetComponent", new[] { typeof(Type) });
            Type unityObject = cameraType;
            while (unityObject != null && unityObject.FullName != "UnityEngine.Object") unityObject = unityObject.BaseType;
            MethodInfo isAlive = unityObject?.GetMethod("op_Implicit", BindingFlags.Static | BindingFlags.Public, null, new[] { unityObject }, null);
            if (gameObject == null || cameraType == null || getComponent == null || isAlive == null)
                throw new InvalidOperationException("camera destruction verification contract is unavailable");
            for (int attempt = 0; attempt < 20; attempt++)
            {
                object component = getComponent.Invoke(gameObject, new object[] { cameraType });
                if (component == null || !(isAlive.Invoke(null, new[] { component }) is bool alive) || !alive) return;
                await Task.Delay(16);
            }
            throw new InvalidOperationException("previous player camera was not destroyed within the bounded frame wait");
        }

        object ResolvePlayerDictionaryKey()
        {
            object match = null;
            int matches = 0;
            foreach (DictionaryEntry entry in players)
            {
                if (!ReferenceEquals(entry.Value, originalPlayer)) continue;
                match = entry.Key;
                matches++;
            }
            if (matches != 1) throw new InvalidOperationException("original local-player dictionary entry count is " + matches);
            return match;
        }

        async Task<object> RemoveCurrentCullingSampler()
        {
            object sampler = contract.CullingSamplerInstance.GetValue(null, null);
            if (sampler == null) throw new InvalidOperationException("active cross-scene culling sampler is unavailable");
            Type unityObject = sampler.GetType();
            while (unityObject != null && unityObject.FullName != "UnityEngine.Object") unityObject = unityObject.BaseType;
            MethodInfo destroy = unityObject?.GetMethod("Destroy", BindingFlags.Static | BindingFlags.Public, null, new[] { unityObject }, null);
            if (destroy == null) throw new InvalidOperationException("cross-scene culling sampler destruction contract is unavailable");
            destroy.Invoke(null, new[] { sampler });
            for (int attempt = 0; attempt < 120; attempt++)
            {
                await Task.Delay(16);
                if (contract.CullingSamplerInstance.GetValue(null, null) == null) return sampler;
            }
            throw new InvalidOperationException("retired cross-scene culling sampler did not shut down within the bounded frame wait");
        }

        async Task WaitForReplacementCullingSampler(object retiredSampler)
        {
            for (int attempt = 0; attempt < 120; attempt++)
            {
                await Task.Delay(16);
                object sampler = contract.CullingSamplerInstance.GetValue(null, null);
                if (sampler != null && !ReferenceEquals(sampler, retiredSampler)) return;
            }
            throw new InvalidOperationException("replacement cross-scene culling sampler did not initialize within the bounded frame wait");
        }

        void ClearCullingCameraState()
        {
            object manager = contract.CullingManagerInstance.GetValue(null, null);
            if (manager == null) throw new InvalidOperationException("CullingManager.Instance is unavailable");
            contract.FinishCullingJobs.Invoke(manager, null);
            contract.ClearCullingCameraData.Invoke(manager, null);
        }

        void RegisterNewCullingCamera(object cameraController)
        {
            object manager = contract.CullingManagerInstance.GetValue(null, null);
            object camera = ReadProperty(cameraController, "Camera");
            if (manager == null || camera == null) throw new InvalidOperationException("replacement culling camera is unavailable");
            contract.RegisterCullingCamera.Invoke(manager, new[] { camera });
        }

        void RefreshDynamicMapsPlayerMarker()
        {
            try
            {
                Type screenType = FindType("DynamicMaps.UI.ModdedMapScreen");
                if (screenType == null) return;
                Type resources = Type.GetType("UnityEngine.Resources, UnityEngine.CoreModule", false);
                MethodInfo findAll = resources?.GetMethod("FindObjectsOfTypeAll", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(Type) }, null);
                var screens = findAll?.Invoke(null, new object[] { screenType }) as IEnumerable;
                int refreshed = 0;
                if (screens != null)
                {
                    foreach (object screen in screens)
                    {
                        object providers = ReadField(screen, "_dynamicMarkerProviders");
                        object mapView = ReadField(screen, "_mapView");
                        if (!(providers is IDictionary dictionary) || mapView == null) continue;
                        foreach (DictionaryEntry entry in dictionary)
                        {
                            object provider = entry.Value;
                            if (provider?.GetType().FullName != "DynamicMaps.DynamicMarkers.PlayerMarkerProvider") continue;
                            MethodInfo remove = provider.GetType().GetMethod("TryRemoveMarker", BindingFlags.Instance | BindingFlags.NonPublic);
                            MethodInfo add = provider.GetType().GetMethod("TryAddMarker", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (remove == null || add == null) continue;
                            remove.Invoke(provider, null);
                            add.Invoke(provider, new[] { mapView });
                            refreshed++;
                        }
                    }
                }
                trace?.Invoke("Recovery trace: Dynamic Maps player marker refresh count=" + refreshed);
            }
            catch (Exception exception)
            {
                trace?.Invoke("Recovery trace: Dynamic Maps player marker refresh warning: " + Unwrap(exception).Message);
            }
        }

        void ValidateAttachment(object gameWorld, object newPlayer)
        {
            if (!ReferenceEquals(ReadField(gameWorld, "MainPlayer"), newPlayer))
                throw new InvalidOperationException("recovered player did not become GameWorld.MainPlayer");
            if (!(ReadField(gameWorld, "RegisteredPlayers") is IEnumerable registered))
                throw new InvalidOperationException("GameWorld registered-player collection is unavailable");
            int matches = 0;
            foreach (object player in registered)
                if (ReferenceEquals(player, newPlayer)) matches++;
            if (matches != 1)
                throw new InvalidOperationException("recovered player registration count is " + matches);
            if (!ReferenceEquals(contract.GamePlayerOwnerMyPlayer.GetValue(null), newPlayer))
                throw new InvalidOperationException("recovered owner did not retain GamePlayerOwner.MyPlayer");
        }

        static void DisposeStrict(object instance)
        {
            if (instance == null) return;
            MethodInfo dispose = instance.GetType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (dispose == null) throw new InvalidOperationException("player disposal contract is unavailable");
            dispose.Invoke(instance, null);
        }

        static void TryDeactivate(object instance)
        {
            if (instance == null) return;
            object gameObject = ReadProperty(instance, "gameObject");
            MethodInfo setActive = gameObject?.GetType().GetMethod("SetActive", new[] { typeof(bool) });
            if (setActive == null) throw new InvalidOperationException("player GameObject deactivation contract is unavailable");
            setActive.Invoke(gameObject, new object[] { false });
        }

        static Exception Unwrap(Exception exception) =>
            exception is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : exception;

        static object ReadProperty(object instance, string name) => instance?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance, null);

        static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        static object ReadField(object instance, string name)
        {
            Type type = instance?.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(instance);
                type = type.BaseType;
            }
            return null;
        }

        static void TryCleanupOwner(object owner)
        {
            if (owner == null) return;
            try
            {
                owner.GetType().GetMethod("CleanupOnDestroy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(owner, null);
            }
            catch { }
        }
    }
}
