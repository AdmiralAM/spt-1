using System;
using System.Collections;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class FastAccessSlotPolicy
    {
        internal static string[] Extend(string[] source)
        {
            if (source == null) return null;

            int existing = -1;
            for (int i = 0; i < source.Length; i++)
            {
                if (string.Equals(source[i], BeltSlotPlan.ArmBand, StringComparison.Ordinal))
                {
                    existing = i;
                    break;
                }
            }

            if (existing >= 0)
            {
                string[] copy = new string[source.Length];
                Array.Copy(source, copy, source.Length);
                return copy;
            }

            string[] result = new string[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[result.Length - 1] = BeltSlotPlan.ArmBand;
            return result;
        }
    }

    internal sealed class FastAccessSlotPatches : IDisposable
    {
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        FieldInfo fastAccessSlotsField;
        FieldInfo bindAvailableSlotsField;
        object originalFastAccessSlots;
        object originalBindAvailableSlots;
        bool installed;

        internal FastAccessSlotPatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                Type inventoryType = ReflectionTools.FindType("EFT.InventoryLogic.Inventory");
                Type slotEnumType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                if (inventoryType == null || slotEnumType == null)
                    return Fail("SPT 4.1 Inventory/EquipmentSlot was not found; belt fast-access slot compatibility is disabled.");

                fastAccessSlotsField = inventoryType.GetField("FastAccessSlots", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                bindAvailableSlotsField = inventoryType.GetField("BindAvailableSlotsExtended", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (!IsSlotArray(fastAccessSlotsField, slotEnumType) || !IsSlotArray(bindAvailableSlotsField, slotEnumType))
                    return Fail("SPT 4.1 fast-access slot arrays changed shape; belt fast-access slot compatibility is disabled.");

                object armBand = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand, false);
                originalFastAccessSlots = fastAccessSlotsField.GetValue(null);
                originalBindAvailableSlots = bindAvailableSlotsField.GetValue(null);
                object extendedFastAccess = AppendSlot(originalFastAccessSlots as Array, slotEnumType, armBand);
                object extendedBindAvailable = AppendSlot(originalBindAvailableSlots as Array, slotEnumType, armBand);
                if (extendedFastAccess == null || extendedBindAvailable == null)
                    return Fail("SPT 4.1 fast-access slot arrays could not be extended safely; belt fast-access slot compatibility is disabled.");

                fastAccessSlotsField.SetValue(null, extendedFastAccess);
                bindAvailableSlotsField.SetValue(null, extendedBindAvailable);
                installed = true;

                if (logInfo != null) logInfo("Belt/Armband Inventory fast-access/reachability slot compatibility installed.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Belt fast-access slot compatibility installation failed safely: " + Unwrap(exception).Message);
            }
        }

        static bool IsSlotArray(FieldInfo field, Type slotEnumType)
        {
            return field != null && field.FieldType.IsArray && field.FieldType.GetElementType() == slotEnumType;
        }

        static Array AppendSlot(Array source, Type slotEnumType, object armBand)
        {
            if (source == null || slotEnumType == null || armBand == null) return null;

            for (int i = 0; i < source.Length; i++)
            {
                if (Equals(source.GetValue(i), armBand))
                {
                    Array clone = Array.CreateInstance(slotEnumType, source.Length);
                    Array.Copy(source, clone, source.Length);
                    return clone;
                }
            }

            Array result = Array.CreateInstance(slotEnumType, source.Length + 1);
            Array.Copy(source, result, source.Length);
            result.SetValue(armBand, source.Length);
            return result;
        }

        bool Fail(string message)
        {
            if (logWarning != null) logWarning(message);
            return false;
        }

        public void Dispose()
        {
            if (!installed) return;

            try
            {
                if (fastAccessSlotsField != null && originalFastAccessSlots != null)
                    fastAccessSlotsField.SetValue(null, originalFastAccessSlots);
                if (bindAvailableSlotsField != null && originalBindAvailableSlots != null)
                    bindAvailableSlotsField.SetValue(null, originalBindAvailableSlots);
            }
            catch { }

            installed = false;
            fastAccessSlotsField = null;
            bindAvailableSlotsField = null;
            originalFastAccessSlots = null;
            originalBindAvailableSlots = null;
        }

        static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
        }
    }
}
