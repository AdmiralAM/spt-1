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
                    logWarning?.Invoke("B&A&HB HOST DISCOVERY FAIL-CLOSED: EFT.InventoryLogic.EquipmentSlot enum was not found. Belt/HeadBand remain ConceptOnly.");
                    return;
                }

                string[] names = Enum.GetNames(equipmentSlotType);
                List<string> beltCandidates = new List<string>();
                List<string> headBandCandidates = new List<string>();

                for (int i = 0; i < names.Length; i++)
                {
                    string name = names[i];
                    if (Contains(name, "belt") || Contains(name, "waist")) beltCandidates.Add(name);
                    if (Contains(name, "head") || Contains(name, "band") || Contains(name, "face") || Contains(name, "ear")) headBandCandidates.Add(name);
                }

                string exactBeltHost = HostBoundaryPolicy.FindExactHost(AccessoryCategory.Belt, names);
                string exactHeadBandHost = HostBoundaryPolicy.FindExactHost(AccessoryCategory.HeadBand, names);

                logInfo?.Invoke("B&A&HB HOST DISCOVERY: EquipmentSlot count=" + names.Length
                    + "; Belt candidates=" + Format(beltCandidates)
                    + "; HeadBand candidates=" + Format(headBandCandidates) + ".");
                logInfo?.Invoke("B&A&HB HOST DISCOVERY EXACT: Belt=" + (exactBeltHost ?? "<none>")
                    + "; HeadBand=" + (exactHeadBandHost ?? "<none>") + ".");

                // Headwear/FaceCover/Earpiece are deliberately not accepted merely
                // because they look related: silently consuming those slots would alter
                // vanilla helmet/face/ear equipment semantics. A plausible Belt-like
                // enum name is likewise evidence only until its UI/lifecycle contract is proven.
                logInfo?.Invoke("B&A&HB HOST DISCOVERY: Belt=ConceptOnly, HeadBand=ConceptOnly; no runtime slot activation performed.");
            }
            catch (Exception exception)
            {
                logWarning?.Invoke("B&A&HB HOST DISCOVERY FAIL-CLOSED: " + exception.GetType().FullName + ": " + exception.Message
                    + ". Belt/HeadBand remain ConceptOnly.");
            }
        }

        static bool Contains(string value, string token)
        {
            return value != null && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string Format(List<string> values)
        {
            return values == null || values.Count == 0 ? "<none>" : string.Join(",", values.ToArray());
        }
    }
}
