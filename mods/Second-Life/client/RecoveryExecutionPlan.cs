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
            this.paidHealing = paidHealing;
            ProfileId = profileId;
        }

        internal string ProfileId { get; }
        internal string RecoveryEquipmentRootId => inventoryLease.RecoveryEquipmentRootId;
        internal int PaidHealingCost => paidHealing.Cost;
        internal bool CanAffordPaidHealing => paidHealing.CanAfford;
        internal string PaidHealingScanSummary => paidHealing.ScanSummary;

        internal async Task ExecuteAsync()
        {
            if (!inventoryLease.Apply()) throw new InvalidOperationException("profile inventory ownership changed before recovery");

            object newPlayer = null;
            object newOwner = null;
            bool attached = false;
            try
            {
                UnregisterOriginalPlayer();
                var creationTask = playerFactory.DynamicInvoke() as Task;
                if (creationTask == null) throw new InvalidOperationException("player factory did not return a Task");
                await creationTask;
                newPlayer = creationTask.GetType().GetProperty("Result")?.GetValue(creationTask, null);
                if (newPlayer == null) throw new InvalidOperationException("player factory returned no LocalPlayer");
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
                contract.CreatePlayerCamera.Invoke(null, new[] { newPlayer });
                contract.Spawn.Invoke(localGame, null);
                paidHealing.Apply(newPlayer);
                attached = true;
                paidHealing.FinalizeDebit(newPlayer);
                TryDispose(originalPlayer);
                TryCleanupOwner(originalOwner);
            }
            finally
            {
                if (!attached)
                {
                    paidHealing.Rollback();
                    contract.LocalPlayer.SetValue(localGame, originalPlayer);
                    contract.PlayerOwner.SetValue(localGame, originalOwner);
                    players[ProfileId] = originalPlayer;
                    TryDispose(newPlayer);
                    if (!inventoryLease.Rollback())
                        throw new InvalidOperationException("recovery failed and profile inventory rollback was rejected");
                }
            }
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

        void UnregisterOriginalPlayer()
        {
            object gameWorld = originalPlayer.GetType().GetProperty("GameWorld")?.GetValue(originalPlayer, null);
            MethodInfo unregister = gameWorld?.GetType().GetMethod("UnregisterPlayer", BindingFlags.Instance | BindingFlags.Public);
            if (gameWorld == null || unregister == null)
                throw new InvalidOperationException("original player cannot be unregistered from GameWorld");
            unregister.Invoke(gameWorld, new[] { originalPlayer });
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
