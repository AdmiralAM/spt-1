using System;

namespace SPTBeltArmbandInventory
{
    internal static class DedicatedSlotPresentationPolicy
    {
        internal const string VanillaHeadwearSlotId = "Headwear";

        internal static string Caption(string slotId, bool russian)
        {
            if (string.Equals(slotId, RuntimeIdentity.DedicatedBeltWireSlotId, StringComparison.Ordinal))
                return russian ? "ПОЯС" : "BELT";
            if (string.Equals(slotId, RuntimeIdentity.DedicatedHeadBandWireSlotId, StringComparison.Ordinal))
                return russian ? "ПОВЯЗКА НА ГОЛОВУ" : "HEADBAND";
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

        internal static bool ShouldSuppressVanillaHeadwearCompatibility(string slotId, string templateId)
        {
            return string.Equals(slotId, VanillaHeadwearSlotId, StringComparison.Ordinal)
                && string.Equals(templateId, RuntimeIdentity.EmergencyHeadBandItemId, StringComparison.Ordinal);
        }
    }
}
