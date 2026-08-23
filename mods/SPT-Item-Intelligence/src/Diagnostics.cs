using System;

namespace SPTItemIntelligence
{
    public sealed class ItemDecisionDiagnostic
    {
        internal static readonly ItemDecisionDiagnostic Empty = new ItemDecisionDiagnostic(
            string.Empty,
            PriceSource.None,
            0,
            0,
            ItemRequirementDecision.None,
            0,
            0,
            0,
            0,
            0,
            0,
            false,
            string.Empty);

        internal ItemDecisionDiagnostic(
            string templateId,
            PriceSource bestPriceSource,
            long totalValue,
            long valuePerSlot,
            ItemRequirementDecision decision,
            int ownedCount,
            int questNeededNow,
            int questNeededLater,
            int hideoutNeeded,
            int keepCount,
            int surplusCount,
            bool requiresFoundInRaid,
            string holdReason)
        {
            TemplateId = templateId ?? string.Empty;
            BestPriceSource = bestPriceSource;
            TotalValue = totalValue;
            ValuePerSlot = valuePerSlot;
            Decision = decision;
            OwnedCount = ownedCount;
            QuestNeededNow = questNeededNow;
            QuestNeededLater = questNeededLater;
            HideoutNeeded = hideoutNeeded;
            KeepCount = keepCount;
            SurplusCount = surplusCount;
            RequiresFoundInRaid = requiresFoundInRaid;
            HoldReason = holdReason ?? string.Empty;
        }

        public string TemplateId { get; }
        public PriceSource BestPriceSource { get; }
        public long TotalValue { get; }
        public long ValuePerSlot { get; }
        public ItemRequirementDecision Decision { get; }
        public int OwnedCount { get; }
        public int QuestNeededNow { get; }
        public int QuestNeededLater { get; }
        public int HideoutNeeded { get; }
        public int KeepCount { get; }
        public int SurplusCount { get; }
        public bool RequiresFoundInRaid { get; }
        public string HoldReason { get; }
        public bool HasData => TemplateId.Length != 0;
        public bool HasPriceData => BestPriceSource != PriceSource.None || TotalValue > 0 || ValuePerSlot > 0;
        public bool HasRequirementData => Decision != ItemRequirementDecision.None || KeepCount > 0 || SurplusCount > 0;
    }

    // Diagnostics are generated only on explicit request. Nothing here participates in hover,
    // repaint, polling, snapshot refresh, or other normal runtime hot paths.
    public static class ItemDecisionDiagnostics
    {
        public static ItemDecisionDiagnostic Capture(ItemPresentationState state)
        {
            if (state == null || object.ReferenceEquals(state, ItemPresentationState.Empty) || string.IsNullOrEmpty(state.TemplateId))
                return ItemDecisionDiagnostic.Empty;

            ItemRequirementState requirement = state.Requirement ?? ItemRequirementState.Empty;
            ItemPriceState price = state.Price;

            return new ItemDecisionDiagnostic(
                state.TemplateId,
                price == null ? PriceSource.None : price.BestSource,
                price == null ? 0 : price.TotalValue,
                price == null ? 0 : price.ValuePerSlot,
                requirement.Decision,
                requirement.OwnedCount,
                requirement.QuestNeededNow,
                requirement.QuestNeededLater,
                requirement.HideoutNeeded,
                requirement.KeepCount,
                requirement.SurplusCount,
                requirement.RequiresFoundInRaid,
                requirement.HoldReason);
        }

        public static ItemDecisionDiagnostic Capture(ItemPresentationStore store, string templateId)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            return Capture(store.Get(templateId));
        }
    }
}
