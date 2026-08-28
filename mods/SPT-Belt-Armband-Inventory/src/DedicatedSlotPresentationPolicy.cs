using System;

namespace SPTBeltArmbandInventory
{
    internal static class DedicatedSlotPresentationPolicy
    {
        internal const string VanillaHeadwearSlotId = "Headwear";
        internal const string VanillaArmBandSlotId = "ArmBand";

        internal static string Caption(string slotId, bool russian)
        {
            if (string.Equals(slotId, RuntimeIdentity.DedicatedBeltWireSlotId, StringComparison.Ordinal))
                return russian ? "ПОЯС" : "BELT";
            if (string.Equals(slotId, RuntimeIdentity.DedicatedHeadBandWireSlotId, StringComparison.Ordinal))
                return russian ? "ГОЛ. ПОВЯЗКА" : "HEADBAND";
            return null;
        }

        internal static bool LooksRussian(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '\u0400' && c <= '\u04FF') return true;
            }
            return false;
        }

        internal static bool ResolveRussian(bool? current, string observedCaption, bool systemRussian)
        {
            if (LooksRussian(observedCaption)) return true;
            if (!string.IsNullOrEmpty(observedCaption))
            {
                for (int i = 0; i < observedCaption.Length; i++)
                {
                    char c = observedCaption[i];
                    if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return false;
                }
            }
            return current ?? systemRussian;
        }

        // Emergency HeadBand has one equipment identity only: dedicated pseudo-slot16.
        // It must not remain compatible with the vanilla Headwear or ArmBand hosts even
        // though the runtime type/template ancestry is intentionally based on the proven
        // searchable ArmBand/container family.
        internal static bool ShouldSuppressVanillaHeadwearCompatibility(string slotId, string templateId)
        {
            if (!string.Equals(templateId, RuntimeIdentity.EmergencyHeadBandItemId, StringComparison.Ordinal))
                return false;

            return string.Equals(slotId, VanillaHeadwearSlotId, StringComparison.Ordinal)
                || string.Equals(slotId, VanillaArmBandSlotId, StringComparison.Ordinal);
        }
    }
}
