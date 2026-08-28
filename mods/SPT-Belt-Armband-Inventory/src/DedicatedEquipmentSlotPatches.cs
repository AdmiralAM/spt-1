using System;
using System.Reflection;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class DedicatedEquipmentSlotRuntime
    {
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static Type EquipmentSlotType;

        internal static object BeltSlotKey => Enum.ToObject(EquipmentSlotType, RuntimeIdentity.DedicatedBeltEquipmentSlotValue);
        internal static object HeadBandSlotKey => Enum.ToObject(EquipmentSlotType, RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);

        internal static bool ValidatePseudoSlotBoundary()
        {
            if (EquipmentSlotType == null || !EquipmentSlotType.IsEnum) return false;
            object armBand = Enum.Parse(EquipmentSlotType, "ArmBand", false);
            int armBandValue = Convert.ToInt32(armBand);
            Array values = Enum.GetValues(EquipmentSlotType);
            int max = int.MinValue;
            for (int i = 0; i < values.Length; i++) max = Math.Max(max, Convert.ToInt32(values.GetValue(i)));
            return armBandValue == 14
                && max == 14
                && RuntimeIdentity.DedicatedBeltEquipmentSlotValue == max + 1
                && RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue == max + 2;
        }

        internal static void Reset()
        {
            LogInfo = null;
            LogWarning = null;
            EquipmentSlotType = null;
        }
    }

    internal sealed class DedicatedEquipmentSlotPatches : IDisposable
    {
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        FieldInfo containersOrderField;
        Array originalContainersOrder;
        Array installedContainersOrder;

        internal DedicatedEquipmentSlotPatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                Type equipmentSlotType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type containersPanelType = ReflectionTools.FindType("EFT.UI.ContainersPanel");
                if (equipmentSlotType == null || containersPanelType == null)
                    return Fail("SPT 4.1 dedicated equipment-slot client boundary was not found.");

                DedicatedEquipmentSlotRuntime.EquipmentSlotType = equipmentSlotType;
                DedicatedEquipmentSlotRuntime.LogInfo = logInfo;
                DedicatedEquipmentSlotRuntime.LogWarning = logWarning;
                if (!DedicatedEquipmentSlotRuntime.ValidatePseudoSlotBoundary())
                    return Fail("EquipmentSlot enum no longer ends at ArmBand=14; pseudo-slot values 15/16 refused to avoid collision.");

                containersOrderField = FindContainersOrderField(containersPanelType, equipmentSlotType);
                if (containersOrderField == null)
                    return Fail("ContainersPanel canonical slot-order array was not found exactly; Belt UI projection refused.");
                originalContainersOrder = containersOrderField.GetValue(null) as Array;
                installedContainersOrder = BuildBeltOrder(originalContainersOrder, equipmentSlotType);
                containersOrderField.SetValue(null, installedContainersOrder);

                // HeadBand presentation is intentionally not created from EquipmentTab.Awake.
                // The former early clone copied the full Headwear geometry before native
                // SlotView.Show had settled, producing the first-entry stretched layout seen
                // in physical RC1. DedicatedSlotPresentationPatches now owns the whole slot16
                // visual lifecycle and creates/binds the compact view only from the real
                // Headwear SlotView.Show boundary.
                logInfo?.Invoke("B&A&HB #2 MOD SPT dedicated client slot contract installed: Belt pseudo-slot15 after Pockets; HeadBand pseudo-slot16 presentation deferred entirely to native SlotView.Show.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Dedicated equipment-slot installation failed safely: " + Unwrap(exception).GetType().FullName + ": " + Unwrap(exception).Message);
            }
        }

        static FieldInfo FindContainersOrderField(Type panelType, Type equipmentSlotType)
        {
            FieldInfo[] fields = panelType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!field.FieldType.IsArray || field.FieldType.GetElementType() != equipmentSlotType) continue;
                Array value = field.GetValue(null) as Array;
                if (value == null || value.Length != 5) continue;
                string[] expected = { "TacticalVest", "Pockets", "Backpack", "SecuredContainer", "Dogtag" };
                bool match = true;
                for (int p = 0; p < expected.Length; p++)
                    if (!string.Equals(value.GetValue(p)?.ToString(), expected[p], StringComparison.Ordinal)) { match = false; break; }
                if (match) return field;
            }
            return null;
        }

        static Array BuildBeltOrder(Array source, Type equipmentSlotType)
        {
            if (source == null || source.Length != 5) throw new InvalidOperationException("canonical ContainersPanel order unavailable");
            Array result = Array.CreateInstance(equipmentSlotType, source.Length + 1);
            int target = 0;
            for (int i = 0; i < source.Length; i++)
            {
                object value = source.GetValue(i);
                result.SetValue(value, target++);
                if (string.Equals(value?.ToString(), "Pockets", StringComparison.Ordinal))
                    result.SetValue(DedicatedEquipmentSlotRuntime.BeltSlotKey, target++);
            }
            if (target != result.Length) throw new InvalidOperationException("Pockets anchor missing from canonical ContainersPanel order");
            return result;
        }

        void RestoreContainersOrder()
        {
            try
            {
                if (containersOrderField != null && installedContainersOrder != null && ReferenceEquals(containersOrderField.GetValue(null), installedContainersOrder))
                    containersOrderField.SetValue(null, originalContainersOrder);
            }
            catch { }
            containersOrderField = null;
            originalContainersOrder = null;
            installedContainersOrder = null;
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }

        bool Fail(string message) { logWarning?.Invoke(message); return false; }

        public void Dispose()
        {
            RestoreContainersOrder();
            DedicatedEquipmentSlotRuntime.Reset();
        }
    }
}
