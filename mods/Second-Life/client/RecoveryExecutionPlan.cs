using System;
using System.Collections;
using System.Reflection;
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
            paidHealing.Cancel();
            armamentReservation?.Refund();
        }

        internal async Task ExecuteAsync()
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
            try
            {
                contract.UnregisterWorldPlayer.Invoke(gameWorld, new[] { originalPlayer });
                originalPlayerUnregistered = true;
                contract.DestroyPlayerCamera.Invoke(null, new[] { originalPlayer });
                originalCameraRemoved = true;
                await Task.Delay(50);
                var creationTask = playerFactory.DynamicInvoke() as Task;
                if (creationTask == null) throw new InvalidOperationException("player factory did not return a Task");
                await creationTask;
                newPlayer = creationTask.GetType().GetProperty("Result")?.GetValue(creationTask, null);
                if (newPlayer == null) throw new InvalidOperationException("player factory returned no LocalPlayer");
                // Player.Init registers the player before the factory task completes.
                // Track that ownership immediately so every later failure unregisters it.
                newPlayerRegistered = true;
                RuntimeSafeSpawnSelector.Apply(newPlayer, spawnSelection);
                await RuntimeArmamentService.TransferAsync(
                    newPlayer,
                    inventoryLease.RecoveryEquipment,
                    armament);

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
                paidHealing.FinalizeDebit(newPlayer);
                armamentReservation?.Commit();
                attached = true;
                TryDispose(originalPlayer);
                TryCleanupOwner(originalOwner);
            }
            finally
            {
                if (!attached)
                {
                    paidHealing.Rollback();
                    armamentReservation?.Refund();
                    if (newPlayerRegistered) contract.UnregisterWorldPlayer.Invoke(gameWorld, new[] { newPlayer });
                    contract.LocalPlayer.SetValue(localGame, originalPlayer);
                    contract.PlayerOwner.SetValue(localGame, originalOwner);
                    players[ProfileId] = originalPlayer;
                    if (originalPlayerUnregistered) contract.RegisterWorldPlayer.Invoke(gameWorld, new[] { originalPlayer });
                    if (newCameraCreated) contract.DestroyPlayerCamera.Invoke(null, new[] { newPlayer });
                    if (originalCameraRemoved)
                    {
                        await Task.Delay(50);
                        contract.CreatePlayerCamera.Invoke(null, new[] { originalPlayer });
                    }
                    TryDispose(newPlayer);
                    if (!inventoryLease.Rollback())
                        throw new InvalidOperationException("recovery failed and profile inventory rollback was rejected");
                }
            }
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
