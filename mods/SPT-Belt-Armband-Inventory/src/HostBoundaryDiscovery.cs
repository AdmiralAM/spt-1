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

                logInfo?.Invoke("B&A&HB HOST DISCOVERY: EquipmentSlot count=" + names.Length
                    + "; Belt candidates=" + Format(beltCandidates)
                    + "; HeadBand candidates=" + Format(headBandCandidates) + ".");

                // Discovery is evidence only. Never promote a concept host merely because
                // an enum name looks plausible; runtime activation still requires an exact
                // host contract and physical proof.
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
