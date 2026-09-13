using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace Admiral.SecondLife.Client
{
    internal sealed class RecoveryInventoryLease
    {
        readonly RecoveryRuntimeContract contract;
        readonly object profile;
        readonly object originalInventory;
        readonly object recoveryInventory;
        bool applied;

        RecoveryInventoryLease(
            RecoveryRuntimeContract contract,
            object profile,
            object originalInventory,
            object recoveryInventory,
            string corpseEquipmentRootId,
            string recoveryEquipmentRootId)
        {
            this.contract = contract;
            this.profile = profile;
            this.originalInventory = originalInventory;
            this.recoveryInventory = recoveryInventory;
            CorpseEquipmentRootId = corpseEquipmentRootId;
            RecoveryEquipmentRootId = recoveryEquipmentRootId;
        }

        internal string CorpseEquipmentRootId { get; }
        internal string RecoveryEquipmentRootId { get; }

        internal static bool TryPrepare(
            RecoveryRuntimeContract contract,
            object profile,
            object corpseEquipment,
            out RecoveryInventoryLease lease,
            out string failure)
        {
            lease = null;
            failure = null;
            if (contract == null || profile == null || corpseEquipment == null)
                return Fail("profile or corpse equipment is unavailable", out failure);

            object originalInventory = contract.ProfileInventory.GetValue(profile);
            object originalEquipment = originalInventory == null
                ? null
                : contract.InventoryEquipment.GetValue(originalInventory);
            if (!ReferenceEquals(originalEquipment, corpseEquipment))
                return Fail("profile equipment is not the exact corpse-owned root", out failure);

            string corpseRootId = ReadString(corpseEquipment, "Id");
            object equipmentTemplate = AccessTools.Property(corpseEquipment.GetType(), "Template")?.GetValue(corpseEquipment, null);
            if (string.IsNullOrWhiteSpace(corpseRootId) || equipmentTemplate == null)
                return Fail("corpse equipment identity/template is unavailable", out failure);

            string recoveryRootId = Guid.NewGuid().ToString("N").Substring(0, 24);
            object recoveryEquipment = contract.EquipmentConstructor.Invoke(new[] { recoveryRootId, equipmentTemplate });
            object[] inventoryArguments = BuildInventoryArguments(contract, originalInventory, recoveryEquipment);
            object recoveryInventory = contract.InventoryConstructor.Invoke(inventoryArguments);
            if (recoveryInventory == null || ReferenceEquals(recoveryEquipment, corpseEquipment))
                return Fail("replacement inventory construction failed ownership checks", out failure);

            lease = new RecoveryInventoryLease(
                contract,
                profile,
                originalInventory,
                recoveryInventory,
                corpseRootId,
                recoveryRootId);
            return true;
        }

        internal bool Apply()
        {
            if (applied || !ReferenceEquals(contract.ProfileInventory.GetValue(profile), originalInventory))
                return false;

            contract.ProfileInventory.SetValue(profile, recoveryInventory);
            applied = ReferenceEquals(contract.ProfileInventory.GetValue(profile), recoveryInventory);
            return applied;
        }

        internal bool Rollback()
        {
            if (!applied) return true;
            if (!ReferenceEquals(contract.ProfileInventory.GetValue(profile), recoveryInventory)) return false;

            contract.ProfileInventory.SetValue(profile, originalInventory);
            applied = false;
            return ReferenceEquals(contract.ProfileInventory.GetValue(profile), originalInventory);
        }

        static object[] BuildInventoryArguments(
            RecoveryRuntimeContract contract,
            object inventory,
            object recoveryEquipment)
        {
            ParameterInfo[] parameters = contract.InventoryConstructor.GetParameters();
            return new[]
            {
                recoveryEquipment,
                ReadField(inventory, "Stash"),
                ReadField(inventory, "QuestRaidItems"),
                ReadField(inventory, "QuestStashItems"),
                ReadField(inventory, "SortingTable"),
                ReadField(inventory, "HideoutCustomizationStash"),
                ReadField(inventory, "HideoutAreaStashes"),
                CopyFastAccessIds(inventory, parameters[7].ParameterType),
                ReadField(inventory, "DiscardLimits"),
                CopyFavoriteIds(inventory, parameters[9].ParameterType),
                ReadField(inventory, "DeserializationErrors"),
                ReadBoolean(inventory, "CheckHash")
            };
        }

        static object CopyFastAccessIds(object inventory, Type dictionaryType)
        {
            object copy = Activator.CreateInstance(dictionaryType);
            MethodInfo add = dictionaryType.GetMethod("Add");
            object fastAccess = ReadField(inventory, "FastAccess");
            var boundItems = ReadField(fastAccess, "BoundItems") as IDictionary;
            Type mongoIdType = dictionaryType.GetGenericArguments()[1];
            ConstructorInfo mongoIdConstructor = mongoIdType.GetConstructor(new[] { typeof(string) });
            if (boundItems == null || add == null || mongoIdConstructor == null) return copy;

            foreach (DictionaryEntry entry in boundItems)
            {
                string itemId = ReadString(entry.Value, "Id");
                if (!string.IsNullOrWhiteSpace(itemId))
                    add.Invoke(copy, new[] { entry.Key, mongoIdConstructor.Invoke(new object[] { itemId }) });
            }

            return copy;
        }

        static object CopyFavoriteIds(object inventory, Type listType)
        {
            object copy = Activator.CreateInstance(listType);
            MethodInfo add = listType.GetMethod("Add");
            var favorites = ReadField(inventory, "FavoriteItemsStorage") as IEnumerable;
            if (favorites == null || add == null) return copy;

            foreach (object favorite in favorites) add.Invoke(copy, new[] { favorite });
            return copy;
        }

        static object ReadField(object instance, string fieldName) =>
            instance == null ? null : AccessTools.Field(instance.GetType(), fieldName)?.GetValue(instance);

        static bool ReadBoolean(object instance, string propertyName)
        {
            object value = AccessTools.Property(instance.GetType(), propertyName)?.GetValue(instance, null);
            return value is bool result && result;
        }

        static string ReadString(object instance, string propertyName)
        {
            object value = instance == null
                ? null
                : AccessTools.Property(instance.GetType(), propertyName)?.GetValue(instance, null);
            return value?.ToString();
        }

        static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
