using System;

namespace SPTItemIntelligence
{
    public enum ItemNeedReason
    {
        None,
        ActiveQuest,
        Hideout,
        FutureQuest
    }

    public enum ItemNeedIcon
    {
        None,
        Quest,
        Hideout,
        FutureQuest
    }

    public sealed class ItemIntelligenceDecision
    {
        internal ItemIntelligenceDecision(
            RequirementCoverage coverage,
            ItemNeedReason primary,
            ItemNeedReason secondary,
            int remaining,
            bool candidateFoundInRaid,
            ItemRequirementAllocation allocation)
        {
            Coverage = coverage;
            PrimaryReason = primary;
            SecondaryReason = secondary;
            Remaining = Math.Max(0, remaining);
            CandidateFoundInRaid = candidateFoundInRaid;
            Allocation = allocation;
        }

        public RequirementCoverage Coverage { get; }
        public ItemNeedReason PrimaryReason { get; }
        public ItemNeedReason SecondaryReason { get; }
        public int Remaining { get; }
        public bool CandidateFoundInRaid { get; }
        public ItemRequirementAllocation Allocation { get; }
        public bool ShouldKeepThisItem => PrimaryReason != ItemNeedReason.None && Remaining > 0;
    }

    public sealed class SenseRequirementPresentation
    {
        internal static readonly SenseRequirementPresentation None = new SenseRequirementPresentation(
            ItemNeedIcon.None, ItemNeedReason.None, ItemNeedReason.None, 0);

        internal SenseRequirementPresentation(ItemNeedIcon icon, ItemNeedReason primary, ItemNeedReason outline, int remaining)
        {
            Icon = icon;
            PrimaryReason = primary;
            OutlineReason = outline;
            Remaining = Math.Max(0, remaining);
        }

        public ItemNeedIcon Icon { get; }
        public ItemNeedReason PrimaryReason { get; }
        public ItemNeedReason OutlineReason { get; }
        public int Remaining { get; }
        public bool OverridesSense => Icon != ItemNeedIcon.None && Remaining > 0;
    }

    public static class ItemIntelligenceDecisionEngine
    {
        public static ItemIntelligenceDecision Evaluate(
            ItemRequirementAllocation baseline,
            bool candidateFoundInRaid,
            int raidOwned = 0,
            int raidFoundInRaid = 0)
        {
            if (baseline == null)
                return new ItemIntelligenceDecision(RequirementCoverage.NotNeeded, ItemNeedReason.None,
                    ItemNeedReason.None, 0, candidateFoundInRaid, null);

            int addedOwned = Math.Max(0, raidOwned);
            int addedFir = Math.Min(addedOwned, Math.Max(0, raidFoundInRaid));
            ItemRequirementAllocation allocation = new ItemRequirementAllocation(
                checked(baseline.Owned + addedOwned),
                checked(baseline.OwnedFir + addedFir),
                baseline.NowRequired,
                baseline.LaterRequired,
                baseline.HideoutRequired,
                baseline.NowFirRequired,
                baseline.LaterFirRequired);

            ItemNeedReason primary = ItemNeedReason.None;
            ItemNeedReason secondary = ItemNeedReason.None;
            int remaining = 0;
            AddReason(ItemNeedReason.ActiveQuest, EligibleNowMissing(allocation, candidateFoundInRaid), ref primary, ref secondary, ref remaining);
            AddReason(ItemNeedReason.Hideout, allocation.HideoutMissing, ref primary, ref secondary, ref remaining);
            AddReason(ItemNeedReason.FutureQuest, EligibleLaterMissing(allocation, candidateFoundInRaid), ref primary, ref secondary, ref remaining);

            return new ItemIntelligenceDecision(allocation.Coverage, primary, secondary, remaining, candidateFoundInRaid, allocation);
        }

        static int EligibleNowMissing(ItemRequirementAllocation allocation, bool candidateFoundInRaid)
        {
            if (candidateFoundInRaid) return allocation.NowMissing;
            int unrestrictedRequired = allocation.NowRequired - allocation.NowFirRequired;
            int unrestrictedAllocated = allocation.NowAllocated - allocation.NowFirAllocated;
            return Math.Max(0, unrestrictedRequired - unrestrictedAllocated);
        }

        static int EligibleLaterMissing(ItemRequirementAllocation allocation, bool candidateFoundInRaid)
        {
            if (candidateFoundInRaid) return allocation.LaterMissing;
            int unrestrictedRequired = allocation.LaterRequired - allocation.LaterFirRequired;
            int unrestrictedAllocated = allocation.LaterAllocated - allocation.LaterFirAllocated;
            return Math.Max(0, unrestrictedRequired - unrestrictedAllocated);
        }

        static void AddReason(ItemNeedReason reason, int missing, ref ItemNeedReason primary, ref ItemNeedReason secondary, ref int remaining)
        {
            if (missing <= 0) return;
            if (primary == ItemNeedReason.None)
            {
                primary = reason;
                remaining = missing;
            }
            else if (secondary == ItemNeedReason.None) secondary = reason;
        }
    }

    public static class SenseRequirementMapper
    {
        public static SenseRequirementPresentation Map(ItemIntelligenceDecision decision)
        {
            if (decision == null || !decision.ShouldKeepThisItem) return SenseRequirementPresentation.None;
            ItemNeedIcon icon = decision.PrimaryReason == ItemNeedReason.ActiveQuest ? ItemNeedIcon.Quest :
                decision.PrimaryReason == ItemNeedReason.Hideout ? ItemNeedIcon.Hideout :
                decision.PrimaryReason == ItemNeedReason.FutureQuest ? ItemNeedIcon.FutureQuest : ItemNeedIcon.None;
            return icon == ItemNeedIcon.None
                ? SenseRequirementPresentation.None
                : new SenseRequirementPresentation(icon, decision.PrimaryReason, decision.SecondaryReason, decision.Remaining);
        }
    }
}
