using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal static class HostBoundaryPolicy
    {
        internal static string FindExactHost(AccessoryCategory category, IEnumerable<string> slotNames)
        {
            if (slotNames == null) return null;
            foreach (string name in slotNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (category == AccessoryCategory.Belt && EqualsAny(name, "Belt", "Waist", "WaistBelt")) return name;
                if (category == AccessoryCategory.HeadBand && EqualsAny(name, "HeadBand", "Headband")) return name;
            }
            return null;
        }

        internal static bool IsSafeExactHost(AccessoryCategory category, string slotName)
        {
            return string.Equals(FindExactHost(category, new[] { slotName }), slotName, StringComparison.Ordinal);
        }

        static bool EqualsAny(string value, params string[] candidates)
        {
            if (value == null || candidates == null) return false;
            for (int i = 0; i < candidates.Length; i++)
                if (string.Equals(value, candidates[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
