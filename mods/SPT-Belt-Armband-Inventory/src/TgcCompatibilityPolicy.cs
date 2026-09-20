using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal static class TgcCompatibilityPolicy
    {
        internal const string Release = "3.0.0";
        internal const int IntegrationSourcePr = 362;
        internal const string IntegrationContractHead = "bd1500b86c356f5e97fade75cf0c1df974ae9621";

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

        internal static bool TryNormalizeBeltHostFilters(
            ISet<string> armBandFilter,
            ISet<string> beltFilter,
            IEnumerable<string> publishedTemplateIds,
            out int mutations)
        {
            if (armBandFilter == null) throw new ArgumentNullException(nameof(armBandFilter));
            if (beltFilter == null) throw new ArgumentNullException(nameof(beltFilter));
            if (publishedTemplateIds == null) throw new ArgumentNullException(nameof(publishedTemplateIds));
            mutations = 0;
            if (!ExternalCompatibilityApi.IsTgc300Claimed) return true;
            var published = new HashSet<string>(publishedTemplateIds, StringComparer.Ordinal);
            int count = 0;
            foreach (string id in BeltTemplateIds) if (published.Contains(id)) count++;
            if (count == 0) return true;
            if (count != BeltTemplateIds.Count) return false;
            foreach (string id in BeltTemplateIds)
            {
                if (armBandFilter.Remove(id)) mutations++;
                if (beltFilter.Add(id)) mutations++;
            }
            return true;
        }

        internal static bool TryNormalizeSecureContainerFilter(
            ISet<string> filter,
            IEnumerable<string> publishedTemplateIds,
            bool supportedGamma,
            out int mutations)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (publishedTemplateIds == null) throw new ArgumentNullException(nameof(publishedTemplateIds));
            mutations = 0;
            if (!ExternalCompatibilityApi.IsTgc300Claimed) return true;
            var published = new HashSet<string>(publishedTemplateIds, StringComparer.Ordinal);
            int managedCount = 0;
            foreach (string id in SecureContainerPouchAllowlist) if (published.Contains(id)) managedCount++;
            if (published.Contains(ToolBoxTemplateId)) managedCount++;
            if (managedCount == 0) return true;
            if (managedCount != SecureContainerPouchAllowlist.Count + 1) return false;

            if (filter.Remove(ToolBoxTemplateId)) mutations++;
            if (supportedGamma)
            {
                foreach (string id in SecureContainerPouchAllowlist)
                    if (filter.Add(id)) mutations++;
            }
            else
            {
                foreach (string pouchId in SecureContainerPouchAllowlist)
                    if (filter.Remove(pouchId)) mutations++;
            }
            return true;
        }

        internal static bool IsBelt(string templateId)
        {
            if (!ExternalCompatibilityApi.IsTgc300Claimed || string.IsNullOrEmpty(templateId)) return false;
            foreach (string id in BeltTemplateIds)
                if (string.Equals(templateId, id, StringComparison.Ordinal)) return true;
            return false;
        }

        internal static bool IsExplicitSecureContainerPouch(string templateId)
        {
            if (!ExternalCompatibilityApi.IsTgc300Claimed || string.IsNullOrEmpty(templateId)) return false;
            foreach (string id in SecureContainerPouchAllowlist)
                if (string.Equals(templateId, id, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
