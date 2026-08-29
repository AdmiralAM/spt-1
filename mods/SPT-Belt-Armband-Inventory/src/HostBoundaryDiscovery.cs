using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal static class HostBoundaryDiscovery
    {
        internal static void Log(Action<string> logInfo, Action<string> logWarning)
        {
            try
            {
                Type equipmentSlotType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                if (equipmentSlotType == null || !equipmentSlotType.IsEnum)
                {
                    logWarning?.Invoke("B&A&HB SLOT IMPLEMENTATION DISCOVERY FAIL-CLOSED: EFT.InventoryLogic.EquipmentSlot enum was not found. Dedicated Belt/HeadBand identities remain fixed but inactive.");
                    return;
                }

                string[] names = Enum.GetNames(equipmentSlotType);
                List<string> anchors = new List<string>();
                for (int i = 0; i < names.Length; i++)
                {
                    string name = names[i];
                    if (string.Equals(name, DedicatedWearableSlotContract.Belt.UiAnchor, StringComparison.Ordinal)
                        || string.Equals(name, "Backpack", StringComparison.Ordinal)
                        || string.Equals(name, DedicatedWearableSlotContract.HeadBand.UiAnchor, StringComparison.Ordinal))
                        anchors.Add(name);
                }

                bool pockets = ContainsExact(names, "Pockets");
                bool backpack = ContainsExact(names, "Backpack");
                bool headwear = ContainsExact(names, "Headwear");

                logInfo?.Invoke("B&A&HB SLOT IMPLEMENTATION DISCOVERY: EquipmentSlot count=" + names.Length
                    + "; required vanilla UI anchors=" + Format(anchors) + ".");
                logInfo?.Invoke("B&A&HB DEDICATED SLOT CONTRACT: Belt=" + DedicatedWearableSlotContract.BeltSlotId
                    + " after Pockets=" + pockets + ", before Backpack=" + backpack
                    + "; HeadBand=" + DedicatedWearableSlotContract.HeadBandSlotId
                    + " before Headwear=" + headwear + ".");

                if (!pockets || !backpack || !headwear)
                    logWarning?.Invoke("B&A&HB SLOT IMPLEMENTATION DISCOVERY FAIL-CLOSED: one or more required UI anchors are absent; dedicated slot injection must not activate.");
            }
            catch (Exception exception)
            {
                logWarning?.Invoke("B&A&HB SLOT IMPLEMENTATION DISCOVERY FAIL-CLOSED: " + exception.GetType().FullName + ": " + exception.Message
                    + ". Dedicated slot identities remain fixed but inactive.");
            }
        }

        static bool ContainsExact(string[] values, string target)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Length; i++)
                if (string.Equals(values[i], target, StringComparison.Ordinal)) return true;
            return false;
        }

        static string Format(List<string> values)
        {
            return values == null || values.Count == 0 ? "<none>" : string.Join(",", values.ToArray());
        }
    }
}
