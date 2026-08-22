using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SPTItemIntelligence
{
    public enum RequirementScope
    {
        ActiveQuest,
        SelectedHideoutTarget,
        NextHideoutUpgrade,
        NearFutureQuest,
        Wishlist,
        Barter,
        Craft
    }

    public enum ItemDecision
    {
        NoRequirement,
        Keep,
        SafeToSell
    }

    public sealed class ItemRequirement
    {
        public ItemRequirement(
            RequirementScope scope,
            string reason,
            int requiredCount,
            bool foundInRaidRequired = false,
            int prerequisiteDistance = 0,
            bool enabled = true)
        {
            Scope = scope;
            Reason = Normalize(reason, scope.ToString());
            RequiredCount = Math.Max(0, requiredCount);
            FoundInRaidRequired = foundInRaidRequired;
            PrerequisiteDistance = Math.Max(0, prerequisiteDistance);
            Enabled = enabled;
        }

        public RequirementScope Scope { get; }
        public string Reason { get; }
        public int RequiredCount { get; }
        public bool FoundInRaidRequired { get; }
        public int PrerequisiteDistance { get; }
        public bool Enabled { get; }

        static string Normalize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            return value.Trim();
        }
    }

    public sealed class ItemRequirementSnapshot
    {
        readonly ReadOnlyCollection<ItemRequirement> requirements;

        public ItemRequirementSnapshot(string templateId, int ownedTotal, int ownedFoundInRaid, IEnumerable<ItemRequirement> requirements)
        {
            TemplateId = NormalizeId(templateId);
            OwnedTotal = Math.Max(0, ownedTotal);
            OwnedFoundInRaid = Math.Min(OwnedTotal, Math.Max(0, ownedFoundInRaid));

            List<ItemRequirement> values = new List<ItemRequirement>();
            if (requirements != null)
                foreach (ItemRequirement requirement in requirements)
                    if (requirement != null) values.Add(requirement);
            this.requirements = values.AsReadOnly();
        }

        public string TemplateId { get; }
        public int OwnedTotal { get; }
        public int OwnedFoundInRaid { get; }
        public IReadOnlyList<ItemRequirement> Requirements => requirements;

        static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }

    public sealed class SafeToSellPolicy
    {
        public bool IncludeActiveQuests { get; set; } = true;
        public bool IncludeSelectedHideoutTarget { get; set; } = true;
        public bool IncludeNextHideoutUpgrades { get; set; } = true;
        public bool IncludeNearFutureQuests { get; set; } = true;
        public int NearFutureQuestDepth { get; set; } = 2;
        public bool IncludeWishlist { get; set; } = true;
        public bool IncludeBarters { get; set; }
        public bool IncludeCrafts { get; set; }

        internal bool Includes(ItemRequirement requirement)
        {
            if (requirement == null || !requirement.Enabled || requirement.RequiredCount <= 0) return false;
            switch (requirement.Scope)
            {
                case RequirementScope.ActiveQuest: return IncludeActiveQuests;
                case RequirementScope.SelectedHideoutTarget: return IncludeSelectedHideoutTarget;
                case RequirementScope.NextHideoutUpgrade: return IncludeNextHideoutUpgrades;
                case RequirementScope.NearFutureQuest:
                    return IncludeNearFutureQuests && requirement.PrerequisiteDistance <= Math.Max(0, NearFutureQuestDepth);
                case RequirementScope.Wishlist: return IncludeWishlist;
                case RequirementScope.Barter: return IncludeBarters;
                case RequirementScope.Craft: return IncludeCrafts;
                default: return false;
            }
        }
    }

    public sealed class RequirementAllocation
    {
        public RequirementAllocation(ItemRequirement requirement, int protectedOwned, int missing)
        {
            Requirement = requirement;
            ProtectedOwned = Math.Max(0, protectedOwned);
            Missing = Math.Max(0, missing);
        }

        public ItemRequirement Requirement { get; }
        public int ProtectedOwned { get; }
        public int Missing { get; }
    }

    public sealed class SafeToSellResult
    {
        readonly ReadOnlyCollection<RequirementAllocation> allocations;

        internal SafeToSellResult(
            ItemRequirementSnapshot snapshot,
            IList<RequirementAllocation> allocations,
            int protectedOwned,
            int missingFoundInRaid,
            int missingFlexible,
            int safeSurplus)
        {
            TemplateId = snapshot.TemplateId;
            OwnedTotal = snapshot.OwnedTotal;
            OwnedFoundInRaid = snapshot.OwnedFoundInRaid;
            ProtectedOwned = protectedOwned;
            MissingFoundInRaid = missingFoundInRaid;
            MissingFlexible = missingFlexible;
            SafeSurplus = safeSurplus;
            this.allocations = new List<RequirementAllocation>(allocations).AsReadOnly();

            HighestPriorityReason = this.allocations.Count == 0 ? null : this.allocations[0].Requirement;
            if (SafeSurplus > 0) Decision = ItemDecision.SafeToSell;
            else if (ProtectedOwned > 0 || MissingTotal > 0) Decision = ItemDecision.Keep;
            else Decision = ItemDecision.NoRequirement;
        }

        public string TemplateId { get; }
        public int OwnedTotal { get; }
        public int OwnedFoundInRaid { get; }
        public int ProtectedOwned { get; }
        public int MissingFoundInRaid { get; }
        public int MissingFlexible { get; }
        public int MissingTotal => MissingFoundInRaid + MissingFlexible;
        public int SafeSurplus { get; }
        public ItemDecision Decision { get; }
        public ItemRequirement HighestPriorityReason { get; }
        public IReadOnlyList<RequirementAllocation> Allocations => allocations;

        public string Summary
        {
            get
            {
                if (Decision == ItemDecision.SafeToSell) return "SAFE TO SELL: " + SafeSurplus;
                if (Decision == ItemDecision.Keep && HighestPriorityReason != null)
                {
                    int keep = ProtectedOwned > 0 ? ProtectedOwned : MissingTotal;
                    return "KEEP " + keep + " — " + HighestPriorityReason.Reason;
                }
                return "NO CURRENT/NEAR REQUIREMENT";
            }
        }
    }

    public sealed class SafeToSellEvaluator
    {
        public SafeToSellResult Evaluate(ItemRequirementSnapshot snapshot, SafeToSellPolicy policy = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            policy = policy ?? new SafeToSellPolicy();

            List<IndexedRequirement> included = new List<IndexedRequirement>();
            for (int i = 0; i < snapshot.Requirements.Count; i++)
            {
                ItemRequirement requirement = snapshot.Requirements[i];
                if (policy.Includes(requirement)) included.Add(new IndexedRequirement(requirement, i));
            }
            included.Sort(Compare);

            int availableFoundInRaid = snapshot.OwnedFoundInRaid;
            int availableFlexible = snapshot.OwnedTotal - snapshot.OwnedFoundInRaid;
            int protectedOwned = 0;
            int missingFoundInRaid = 0;
            int missingFlexible = 0;
            List<RequirementAllocation> allocations = new List<RequirementAllocation>(included.Count);

            for (int i = 0; i < included.Count; i++)
            {
                ItemRequirement requirement = included[i].Requirement;
                int protectedForReason;
                int missing;

                if (requirement.FoundInRaidRequired)
                {
                    protectedForReason = Math.Min(availableFoundInRaid, requirement.RequiredCount);
                    availableFoundInRaid -= protectedForReason;
                    missing = requirement.RequiredCount - protectedForReason;
                    missingFoundInRaid += missing;
                }
                else
                {
                    int fromFlexible = Math.Min(availableFlexible, requirement.RequiredCount);
                    availableFlexible -= fromFlexible;
                    int remaining = requirement.RequiredCount - fromFlexible;
                    int fromFoundInRaid = Math.Min(availableFoundInRaid, remaining);
                    availableFoundInRaid -= fromFoundInRaid;
                    protectedForReason = fromFlexible + fromFoundInRaid;
                    missing = requirement.RequiredCount - protectedForReason;
                    missingFlexible += missing;
                }

                protectedOwned += protectedForReason;
                allocations.Add(new RequirementAllocation(requirement, protectedForReason, missing));
            }

            int safeSurplus = availableFoundInRaid + availableFlexible;
            return new SafeToSellResult(snapshot, allocations, protectedOwned, missingFoundInRaid, missingFlexible, safeSurplus);
        }

        static int Compare(IndexedRequirement left, IndexedRequirement right)
        {
            int priority = Priority(left.Requirement).CompareTo(Priority(right.Requirement));
            if (priority != 0) return priority;
            int distance = left.Requirement.PrerequisiteDistance.CompareTo(right.Requirement.PrerequisiteDistance);
            if (distance != 0) return distance;
            return left.Index.CompareTo(right.Index);
        }

        static int Priority(ItemRequirement requirement)
        {
            if (requirement.Scope == RequirementScope.ActiveQuest && requirement.FoundInRaidRequired) return 0;
            switch (requirement.Scope)
            {
                case RequirementScope.ActiveQuest: return 1;
                case RequirementScope.SelectedHideoutTarget: return 2;
                case RequirementScope.NextHideoutUpgrade: return 3;
                case RequirementScope.NearFutureQuest: return 4;
                case RequirementScope.Wishlist: return 5;
                case RequirementScope.Barter: return 6;
                case RequirementScope.Craft: return 7;
                default: return int.MaxValue;
            }
        }

        sealed class IndexedRequirement
        {
            public IndexedRequirement(ItemRequirement requirement, int index)
            {
                Requirement = requirement;
                Index = index;
            }

            public ItemRequirement Requirement { get; }
            public int Index { get; }
        }
    }
}
