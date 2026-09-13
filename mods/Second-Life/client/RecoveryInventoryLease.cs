using System;
using System.Collections;
using System.Linq;
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
        readonly System.Collections.Generic.List<SlotTransfer> protectedTransfers;
        bool applied;

        RecoveryInventoryLease(
            RecoveryRuntimeContract contract,
            object profile,
            object originalInventory,
            object recoveryInventory,
            System.Collections.Generic.List<SlotTransfer> protectedTransfers,
            string corpseEquipmentRootId,
            string recoveryEquipmentRootId)
        {
            this.contract = contract;
            this.profile = profile;
            this.originalInventory = originalInventory;
            this.recoveryInventory = recoveryInventory;
            this.protectedTransfers = protectedTransfers;
            CorpseEquipmentRootId = corpseEquipmentRootId;
            RecoveryEquipmentRootId = recoveryEquipmentRootId;
        }

        internal string CorpseEquipmentRootId { get; }
        internal string RecoveryEquipmentRootId { get; }
        internal object RecoveryEquipment => contract.InventoryEquipment.GetValue(recoveryInventory);

        internal bool TryAttachArmament(RuntimeArmament armament, out string failure)
        {
            failure = null;
            if (armament == null) return true;
            if (!armament.DetachedRoots) return Fail("reserved armament is not detached from the authoritative stash", out failure);
            try
            {
                Type equipmentSlot = FindType("EFT.InventoryLogic.EquipmentSlot");
                MethodInfo getSlot = RecoveryEquipment.GetType().GetMethod("GetSlot", new[] { equipmentSlot });
                object holster = getSlot?.Invoke(RecoveryEquipment, new[] { Enum.Parse(equipmentSlot, "Holster") });
                MethodInfo attach = holster?.GetType().GetMethod("ChangeContainedItemDirectly", BindingFlags.Instance | BindingFlags.Public);
                if (attach == null) return Fail("recovery holster direct-attachment contract is unavailable", out failure);
                attach.Invoke(holster, new[] { armament.Pistol });

                object pocketsSlot = getSlot.Invoke(RecoveryEquipment, new[] { Enum.Parse(equipmentSlot, "Pockets") });
                object pockets = AccessTools.Property(pocketsSlot.GetType(), "ContainedItem")?.GetValue(pocketsSlot, null);
                object address = FindGridAddress(pockets, armament.SpareMagazine);
                MethodInfo add = address?.GetType().GetMethod("AddWithoutRestrictions", BindingFlags.Instance | BindingFlags.Public);
                object result = add?.Invoke(address, new[] { armament.SpareMagazine });
                if (result == null || !(AccessTools.Property(result.GetType(), "Succeeded")?.GetValue(result, null) is bool succeeded) || !succeeded)
                    return Fail("reserved spare magazine could not be attached to recovery pockets", out failure);
                if (!ReferenceEquals(ReadCurrentItem(holster), armament.Pistol) ||
                    !HasSameContainer(armament.SpareMagazine, address) ||
                    !ReferenceEquals(armament.Pistol.GetType().GetMethod("GetCurrentMagazine")?.Invoke(armament.Pistol, null), armament.InstalledMagazine))
                    return Fail("recovery armament identity changed during direct attachment", out failure);
                return true;
            }
            catch (Exception exception)
            {
                return Fail("recovery armament direct attachment failed: " + (exception.InnerException?.Message ?? exception.Message), out failure);
            }
        }

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

            // Keep the profile's persistent equipment identity on the distinct
            // recovery object. Equipment roots are controller-local anchors; a
            // new random ID leaves SPT's authoritative equipment pointer dangling
            // after the raid result merge.
            string recoveryRootId = corpseRootId;
            object recoveryEquipment = contract.EquipmentConstructor.Invoke(new[] { recoveryRootId, equipmentTemplate });
            if (!TryCreateIntrinsicPockets(originalEquipment, recoveryEquipment))
                return Fail("intrinsic recovery pockets could not be constructed", out failure);
            var protectedTransfers = PrepareProtectedTransfers(originalEquipment, recoveryEquipment);
            object[] inventoryArguments = BuildInventoryArguments(contract, originalInventory, recoveryEquipment);
            object recoveryInventory = contract.InventoryConstructor.Invoke(inventoryArguments);
            if (recoveryInventory == null || ReferenceEquals(recoveryEquipment, corpseEquipment))
                return Fail("replacement inventory construction failed ownership checks", out failure);

            lease = new RecoveryInventoryLease(
                contract,
                profile,
                originalInventory,
                recoveryInventory,
                protectedTransfers,
                corpseRootId,
                recoveryRootId);
            return true;
        }

        internal bool Apply()
        {
            if (applied || !ReferenceEquals(contract.ProfileInventory.GetValue(profile), originalInventory))
                return false;

            try
            {
                foreach (SlotTransfer transfer in protectedTransfers) transfer.MoveToRecovery();
                contract.ProfileInventory.SetValue(profile, recoveryInventory);
                applied = ReferenceEquals(contract.ProfileInventory.GetValue(profile), recoveryInventory);
                if (!applied) throw new InvalidOperationException("replacement inventory assignment was rejected");
                return true;
            }
            catch
            {
                for (int index = protectedTransfers.Count - 1; index >= 0; index--) protectedTransfers[index].MoveToCorpse();
                throw;
            }
        }

        internal bool Rollback()
        {
            if (!applied) return true;
            if (!ReferenceEquals(contract.ProfileInventory.GetValue(profile), recoveryInventory)) return false;

            contract.ProfileInventory.SetValue(profile, originalInventory);
            for (int index = protectedTransfers.Count - 1; index >= 0; index--) protectedTransfers[index].MoveToCorpse();
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

        static bool TryCreateIntrinsicPockets(object originalEquipment, object recoveryEquipment)
        {
            Type equipmentSlot = FindType("EFT.InventoryLogic.EquipmentSlot");
            object pocketsValue = Enum.Parse(equipmentSlot, "Pockets");
            MethodInfo getSlot = originalEquipment.GetType().GetMethod("GetSlot", new[] { equipmentSlot });
            object originalSlot = getSlot?.Invoke(originalEquipment, new[] { pocketsValue });
            object originalPockets = originalSlot == null ? null : AccessTools.Property(originalSlot.GetType(), "ContainedItem")?.GetValue(originalSlot, null);
            string templateId = ReadString(originalPockets, "StringTemplateId");
            if (string.IsNullOrWhiteSpace(templateId)) return false;

            Type itemFactoryType = FindType("EFT.ItemFactory");
            Type singleton = FindType("Comfort.Common.Singleton`1")?.MakeGenericType(itemFactoryType);
            object factory = singleton?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null, null);
            MethodInfo createItem = itemFactoryType?.GetMethod("CreateItem", BindingFlags.Instance | BindingFlags.Public);
            object pockets = createItem?.Invoke(factory, new object[] { Guid.NewGuid().ToString("N").Substring(0, 24), templateId, null });
            object recoverySlot = getSlot?.Invoke(recoveryEquipment, new[] { pocketsValue });
            MethodInfo attach = recoverySlot?.GetType().GetMethod("ChangeContainedItemDirectly", BindingFlags.Instance | BindingFlags.Public);
            if (pockets == null || attach == null) return false;
            attach.Invoke(recoverySlot, new[] { pockets });
            return ReferenceEquals(AccessTools.Property(recoverySlot.GetType(), "ContainedItem")?.GetValue(recoverySlot, null), pockets);
        }

        static System.Collections.Generic.List<SlotTransfer> PrepareProtectedTransfers(object corpseEquipment, object recoveryEquipment)
        {
            Type equipmentSlot = FindType("EFT.InventoryLogic.EquipmentSlot");
            MethodInfo getSlot = corpseEquipment.GetType().GetMethod("GetSlot", new[] { equipmentSlot });
            var transfers = new System.Collections.Generic.List<SlotTransfer>();
            object[] protectedSlots =
            {
                Enum.Parse(equipmentSlot, "SecuredContainer"),
                Enum.Parse(equipmentSlot, "ArmBand"),
                Enum.ToObject(equipmentSlot, 15),
                Enum.ToObject(equipmentSlot, 16)
            };
            foreach (object slotValue in protectedSlots)
            {
                object corpseSlot = getSlot.Invoke(corpseEquipment, new[] { slotValue });
                object recoverySlot = getSlot.Invoke(recoveryEquipment, new[] { slotValue });
                object item = corpseSlot == null ? null : ReadCurrentItem(corpseSlot);
                if (item != null && recoverySlot != null) transfers.Add(new SlotTransfer(corpseSlot, recoverySlot, item));
            }
            return transfers;
        }

        sealed class SlotTransfer
        {
            readonly object corpseSlot;
            readonly object recoverySlot;
            readonly object item;
            readonly MethodInfo corpseAttach;
            readonly MethodInfo recoveryAttach;

            internal SlotTransfer(object corpseSlot, object recoverySlot, object item)
            {
                this.corpseSlot = corpseSlot;
                this.recoverySlot = recoverySlot;
                this.item = item;
                corpseAttach = corpseSlot.GetType().GetMethod("ChangeContainedItemDirectly", BindingFlags.Instance | BindingFlags.Public);
                recoveryAttach = recoverySlot.GetType().GetMethod("ChangeContainedItemDirectly", BindingFlags.Instance | BindingFlags.Public);
                if (corpseAttach == null || recoveryAttach == null) throw new InvalidOperationException("protected-slot transfer contract is unavailable");
            }

            internal void MoveToRecovery()
            {
                corpseAttach.Invoke(corpseSlot, new object[] { null });
                recoveryAttach.Invoke(recoverySlot, new[] { item });
                if (!ReferenceEquals(ReadCurrentItem(recoverySlot), item)) throw new InvalidOperationException("protected item transfer to recovery failed");
            }

            internal void MoveToCorpse()
            {
                recoveryAttach.Invoke(recoverySlot, new object[] { null });
                corpseAttach.Invoke(corpseSlot, new[] { item });
            }
        }

        static object FindGridAddress(object compound, object item)
        {
            if (!(ReadField(compound, "Grids") is IEnumerable grids)) return null;
            foreach (object grid in grids)
            {
                MethodInfo find = grid.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method => method.Name == "FindLocationForItem" && method.GetParameters().Length == 1);
                object address = find?.Invoke(grid, new[] { item });
                if (address != null) return address;
            }
            return null;
        }

        static object ReadCurrentItem(object slot) => AccessTools.Property(slot.GetType(), "ContainedItem")?.GetValue(slot, null);
        static bool HasSameContainer(object item, object expectedAddress)
        {
            object currentAddress = AccessTools.Property(item.GetType(), "CurrentAddress")?.GetValue(item, null);
            object currentContainer = ReadField(currentAddress, "Container");
            object expectedContainer = ReadField(expectedAddress, "Container");
            object currentLocation = ReadField(currentAddress, "LocationInGrid");
            object expectedLocation = ReadField(expectedAddress, "LocationInGrid");
            return currentAddress != null && currentContainer != null &&
                ReferenceEquals(currentContainer, expectedContainer) &&
                Equals(currentLocation, expectedLocation);
        }

        static object CopyFastAccessIds(object inventory, Type dictionaryType)
        {
            // The recovered equipment intentionally omits the first-life gear.
            // Carrying its runtime hotkey IDs into the replacement controller
            // produces dangling FastAccess bindings during LocalPlayer.Init.
            object copy = Activator.CreateInstance(dictionaryType);
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

        static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
