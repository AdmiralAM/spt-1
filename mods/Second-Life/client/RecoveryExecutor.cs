using System;
using System.Collections;
using System.Threading.Tasks;

namespace Admiral.SecondLife.Client
{
    internal sealed class RecoveryExecutor
    {
        readonly RecoveryRuntimeContract contract;
        readonly Func<string> eligiblePistolTemplates;

        internal RecoveryExecutor(RecoveryRuntimeContract contract, Func<string> eligiblePistolTemplates)
        {
            this.contract = contract;
            this.eligiblePistolTemplates = eligiblePistolTemplates;
        }

        internal bool TryPrepare(
            object localGame,
            object corpseEquipment,
            object corpse,
            string expectedProfileId,
            out RecoveryExecutionPlan plan,
            out string failure)
        {
            plan = null;
            failure = null;
            if (localGame == null || localGame.GetType() != contract.LocalGameType)
                return Fail("only the exact solo LocalGame is supported", out failure);

            object profile = contract.GameProfile.GetValue(localGame);
            object originalPlayer = contract.LocalPlayer.GetValue(localGame);
            object originalOwner = contract.PlayerOwner.GetValue(localGame);
            var players = contract.Players.GetValue(localGame) as IDictionary;
            var playerFactory = contract.PlayerFactory.GetValue(localGame) as Delegate;
            var ownerFactory = contract.OwnerFactory.GetValue(localGame) as Delegate;
            string profileId = ReadString(profile, "ProfileId");
            if (profile == null || originalPlayer == null || originalOwner == null || players == null || playerFactory == null || ownerFactory == null)
                return Fail("local-game recovery dependencies are unavailable", out failure);
            if (string.IsNullOrWhiteSpace(profileId) || !string.Equals(profileId, expectedProfileId, StringComparison.Ordinal))
                return Fail("captured player no longer matches the active profile", out failure);

            if (!RecoveryInventoryLease.TryPrepare(contract, profile, corpseEquipment, out RecoveryInventoryLease lease, out failure))
                return false;
            if (!RuntimeSafeSpawnSelector.TrySelect(playerFactory, originalPlayer, corpse, expectedProfileId + ":" + lease.CorpseEquipmentRootId, out RuntimeSpawnSelection spawnSelection, out failure))
                return false;
            if (!RuntimeArmamentService.TrySelect(profile, expectedProfileId.GetHashCode(), eligiblePistolTemplates?.Invoke(), out RuntimeArmament armament))
                return Fail("stash traversal exceeded its bounded limit", out failure);
            if (!RuntimePaidHealing.TryPrepare(profile, originalPlayer, out RuntimePaidHealing paidHealing, out failure))
                return false;

            plan = new RecoveryExecutionPlan(
                contract,
                localGame,
                originalPlayer,
                originalOwner,
                players,
                playerFactory,
                ownerFactory,
                lease,
                spawnSelection,
                armament,
                paidHealing,
                profileId);
            return true;
        }

        internal async void Execute(
            RecoveryExecutionPlan plan,
            Action<string> completed,
            Action<Exception> failed)
        {
            try
            {
                await plan.ExecuteAsync();
                completed?.Invoke(plan.RecoveryEquipmentRootId);
            }
            catch (Exception exception)
            {
                failed?.Invoke(Unwrap(exception));
            }
        }

        static string ReadString(object instance, string propertyName) =>
            instance?.GetType().GetProperty(propertyName)?.GetValue(instance, null)?.ToString();

        static Exception Unwrap(Exception exception) =>
            exception is System.Reflection.TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : exception;

        static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
