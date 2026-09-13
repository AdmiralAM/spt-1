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
            string profileId)
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
            bool originalPlayerUnregistered = false;
            bool newPlayerRegistered = false;
            bool originalCameraRemoved = false;
            bool newCameraCreated = false;
            object gameWorld = ReadProperty(originalPlayer, "GameWorld");
            if (gameWorld == null) throw new InvalidOperationException("active GameWorld is unavailable");
            Exception failure = null;
            try
            {
                contract.UnregisterWorldPlayer.Invoke(gameWorld, new[] { originalPlayer });
                originalPlayerUnregistered = true;
                contract.DestroyPlayerCamera.Invoke(null, new[] { originalPlayer });
                originalCameraRemoved = true;
                await WaitForCameraRemoval(originalPlayer);
                var creationTask = playerFactory.DynamicInvoke() as Task;
                if (creationTask == null) throw new InvalidOperationException("player factory did not return a Task");
                await creationTask;
                newPlayer = creationTask.GetType().GetProperty("Result")?.GetValue(creationTask, null);
                if (newPlayer == null) throw new InvalidOperationException("player factory returned no LocalPlayer");
                // Player.Init registers the player before the factory task completes.
                // Track that ownership immediately so every later failure unregisters it.
                newPlayerRegistered = true;
                RuntimeSafeSpawnSelector.Apply(newPlayer, spawnSelection);
                RuntimeArmamentService.ValidatePreloaded(inventoryLease.RecoveryEquipment, armament);
                newOwner = ownerFactory.DynamicInvoke(newPlayer);
                if (newOwner == null) throw new InvalidOperationException("owner factory returned no PlayerOwner");

                contract.LocalPlayer.SetValue(localGame, newPlayer);
                contract.PlayerOwner.SetValue(localGame, newOwner);
                players[ProfileId] = newPlayer;
                // Player.Init, reached by the captured native factory, already calls
                // GameWorld.RegisterPlayer. Registering it a second time corrupts the
                // RegisteredPlayers list and leaves world/camera consumers ambiguous.
                contract.CreatePlayerCamera.Invoke(null, new[] { newPlayer });
                newCameraCreated = true;
                contract.Spawn.Invoke(localGame, null);
                paidHealing.Apply(newPlayer);
                ValidateAttachment(gameWorld, newPlayer);
                armamentReservation?.Commit();
                paidHealing.FinalizeDebit(newPlayer);
                armamentReservation?.FinalizeReservation();
                if (confirmRecovery == null || !confirmRecovery(RecoveryEquipmentRootId))
                    throw new InvalidOperationException("recovery lifecycle confirmation was rejected");
                attached = true;
                paidHealing.ReleaseDebit();
                armamentReservation?.ReleaseReservation();
                TryDispose(originalPlayer);
                TryCleanupOwner(originalOwner);
            }
            catch (Exception exception)
            {
                failure = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;
            }

            if (!attached)
            {
                var cleanupFailures = new System.Collections.Generic.List<string>();
                TryCleanupStep(() => paidHealing.Rollback(), "payment refund", cleanupFailures);
                TryCleanupStep(() => armamentReservation?.Refund(), "armament refund", cleanupFailures);
                if (newCameraCreated)
                {
                    TryCleanupStep(() => contract.DestroyPlayerCamera.Invoke(null, new[] { newPlayer }), "new camera destroy", cleanupFailures);
                    try { await WaitForCameraRemoval(newPlayer); }
                    catch (Exception exception) { cleanupFailures.Add("new camera removal: " + exception.Message); }
                }
                if (newPlayerRegistered) TryCleanupStep(() => contract.UnregisterWorldPlayer.Invoke(gameWorld, new[] { newPlayer }), "new player unregister", cleanupFailures);
                TryCleanupStep(() => TryCleanupOwner(newOwner), "new owner cleanup", cleanupFailures);
                TryCleanupStep(() => TryDispose(newPlayer), "new player dispose", cleanupFailures);
                TryCleanupStep(() => contract.LocalPlayer.SetValue(localGame, originalPlayer), "local player restore", cleanupFailures);
                TryCleanupStep(() => contract.PlayerOwner.SetValue(localGame, originalOwner), "player owner restore", cleanupFailures);
                TryCleanupStep(() => players[ProfileId] = originalPlayer, "player dictionary restore", cleanupFailures);
                if (originalPlayerUnregistered) TryCleanupStep(() => contract.RegisterWorldPlayer.Invoke(gameWorld, new[] { originalPlayer }), "original player register", cleanupFailures);
                if (originalCameraRemoved) TryCleanupStep(() => contract.CreatePlayerCamera.Invoke(null, new[] { originalPlayer }), "original camera restore", cleanupFailures);
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

        static void ValidateAttachment(object gameWorld, object newPlayer)
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
        }

        static void TryDispose(object instance)
        {
            if (instance == null) return;
            try
            {
                instance.GetType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(instance, null);
            }
            catch { }
        }

        static object ReadProperty(object instance, string name) => instance?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance, null);

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
