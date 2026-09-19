using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal static class TgcCompatibilityPolicy
    {
        internal const string Release = "3.0.0";

        internal static readonly IReadOnlyCollection<string> BeltTemplateIds = new[]
        {
            "672e2e75a16c1d2034c384cf", // combat belt (Brown)
            "672e2e750ea81b3b93b943ac", // combat belt (Black)
            "672e2e75a26efb53bc703d45", // Police Belt
            "672e2e75bfad327651a1a19b", // MULE combat belt
            "672e2e751ff683e9432cb5af"  // MULE combat belt (Black)
        };

        // TGC 3.0.0 exposes three simple containers. Only the two templates that
        // TGC itself marks as secure-container pouches are admitted, and only by
        // these exact IDs. The Tool Box deliberately remains excluded.
        internal static readonly IReadOnlyCollection<string> SecureContainerPouchAllowlist = new[]
        {
            "672e2e758808bacbb9d5abc4", // Ammo Pouch
            "672e2e7526ba61dbb88be7ff"  // First Aid container
        };

        internal const string ToolBoxTemplateId = "672e2e75b0ab4fcbbf7dc471";

        internal static bool IsBelt(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return false;
            foreach (string id in BeltTemplateIds)
                if (string.Equals(templateId, id, StringComparison.Ordinal)) return true;
            return false;
        }

        internal static bool IsExplicitSecureContainerPouch(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return false;
            foreach (string id in SecureContainerPouchAllowlist)
                if (string.Equals(templateId, id, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
