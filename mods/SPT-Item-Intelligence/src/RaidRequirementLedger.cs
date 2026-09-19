using System;
using System.Collections.Generic;

namespace SPTItemIntelligence
{
    public sealed class RaidRequirementLedger
    {
        readonly Dictionary<string, RaidItemRecord> items = new Dictionary<string, RaidItemRecord>(StringComparer.Ordinal);
        readonly Dictionary<string, RaidTemplateCount> totals = new Dictionary<string, RaidTemplateCount>(StringComparer.Ordinal);
        readonly Dictionary<string, RaidInventoryItemSnapshot> initialInventory = new Dictionary<string, RaidInventoryItemSnapshot>(StringComparer.Ordinal);

        public int Revision { get; private set; }
        public int ItemCount => items.Count;
        public bool IsRaidSessionActive { get; private set; }
        public bool HasInitialInventory { get; private set; }

        public bool BeginRaid()
        {
            if (IsRaidSessionActive) return false;
            IsRaidSessionActive = true;
            Revision++;
            return true;
        }

        public bool CaptureInitialInventory(IEnumerable<RaidInventoryItemSnapshot> snapshot)
        {
            if (HasInitialInventory) return false;
            BeginRaid();
            initialInventory.Clear();
            if (snapshot != null)
                foreach (RaidInventoryItemSnapshot item in snapshot)
                    if (item != null && item.ItemId.Length > 0) initialInventory[item.ItemId] = item;
            HasInitialInventory = true;
            Revision++;
            return true;
        }

        public bool ReplaceFromPlayerInventory(IEnumerable<RaidInventoryItemSnapshot> snapshot)
        {
            if (!HasInitialInventory) return CaptureInitialInventory(snapshot);
            Dictionary<string, RaidInventoryItemSnapshot> current = new Dictionary<string, RaidInventoryItemSnapshot>(StringComparer.Ordinal);
            if (snapshot != null)
                foreach (RaidInventoryItemSnapshot item in snapshot)
                    if (item != null && item.ItemId.Length > 0) current[item.ItemId] = item;

            bool changed = false;
            List<string> removed = new List<string>();
            foreach (string id in items.Keys) if (!current.ContainsKey(id)) removed.Add(id);
            for (int i = 0; i < removed.Count; i++) changed |= Remove(removed[i]);
            foreach (RaidInventoryItemSnapshot item in current.Values)
            {
                RaidInventoryItemSnapshot initial;
                int initialCount = initialInventory.TryGetValue(item.ItemId, out initial) && initial.TemplateId == item.TemplateId
                    ? initial.StackCount : 0;
                int acquired = Math.Max(0, item.StackCount - initialCount);
                changed |= acquired > 0 ? Observe(item.ItemId, item.TemplateId, acquired, item.FoundInRaid) : Remove(item.ItemId);
            }
            return changed;
        }

        public bool Observe(string itemId, string templateId, int stackCount, bool foundInRaid)
        {
            string id = Normalize(itemId);
            string template = RequirementContribution.NormalizeId(templateId);
            int count = Math.Max(0, stackCount);
            if (id.Length == 0 || template.Length == 0 || count == 0) return Remove(id);

            RaidItemRecord previous;
            if (items.TryGetValue(id, out previous))
            {
                if (previous.TemplateId == template && previous.StackCount == count && previous.FoundInRaid == foundInRaid) return false;
                Adjust(previous.TemplateId, -previous.StackCount, previous.FoundInRaid ? -previous.StackCount : 0);
            }

            items[id] = new RaidItemRecord(template, count, foundInRaid);
            Adjust(template, count, foundInRaid ? count : 0);
            Revision++;
            return true;
        }

        public bool Remove(string itemId)
        {
            string id = Normalize(itemId);
            RaidItemRecord previous;
            if (id.Length == 0 || !items.TryGetValue(id, out previous)) return false;
            items.Remove(id);
            Adjust(previous.TemplateId, -previous.StackCount, previous.FoundInRaid ? -previous.StackCount : 0);
            Revision++;
            return true;
        }

        public void Reset()
        {
            if (items.Count == 0 && totals.Count == 0 && !IsRaidSessionActive) return;
            items.Clear();
            totals.Clear();
            initialInventory.Clear();
            HasInitialInventory = false;
            IsRaidSessionActive = false;
            Revision++;
        }

        public ItemIntelligenceDecision Evaluate(string templateId, ItemRequirementAllocation baseline, bool candidateFoundInRaid)
        {
            RaidTemplateCount count = Get(templateId);
            return ItemIntelligenceDecisionEngine.Evaluate(baseline, candidateFoundInRaid, count.Owned, count.FoundInRaid);
        }

        public ItemPresentationState Apply(ItemPresentationState baseline)
        {
            if (baseline == null || baseline == ItemPresentationState.Empty || baseline.Requirement == null)
                return baseline ?? ItemPresentationState.Empty;

            RaidTemplateCount raid = Get(baseline.TemplateId);
            if (raid.Owned == 0)
                return IsRaidSessionActive
                    ? new ItemPresentationState(baseline.TemplateId, baseline.Requirement, baseline.Price, 0, 0, true)
                    : baseline;

            ItemRequirementState requirement = baseline.Requirement;
            ItemRequirementAllocation source = requirement.Allocation;
            ItemRequirementAllocation combined = new ItemRequirementAllocation(
                checked(source.Owned + raid.Owned),
                checked(source.OwnedFir + raid.FoundInRaid),
                source.NowRequired,
                source.LaterRequired,
                source.HideoutRequired,
                source.NowFirRequired,
                source.LaterFirRequired,
                checked(source.ExactOwned + raid.Owned),
                checked(source.ExactOwnedFir + raid.FoundInRaid),
                source.HideoutFirRequired);
            ItemRequirementDecision decision = combined.Keep > 0
                ? ItemRequirementDecision.Keep
                : combined.Surplus > 0 ? ItemRequirementDecision.SafeToSell : ItemRequirementDecision.None;
            ItemRequirementState adjusted = new ItemRequirementState(
                requirement.TemplateId,
                combined.Owned,
                requirement.QuestNeededNow,
                requirement.QuestNeededLater,
                requirement.HideoutNeeded,
                combined.Keep,
                combined.Surplus,
                requirement.Reasons,
                decision,
                requirement.HoldReason,
                requirement.Details,
                combined);
            return new ItemPresentationState(baseline.TemplateId, adjusted, baseline.Price,
                raid.Owned, raid.FoundInRaid, IsRaidSessionActive);
        }

        public RaidTemplateCount Get(string templateId)
        {
            string template = RequirementContribution.NormalizeId(templateId);
            RaidTemplateCount result;
            return template.Length > 0 && totals.TryGetValue(template, out result) ? result : RaidTemplateCount.Empty;
        }

        void Adjust(string templateId, int ownedDelta, int firDelta)
        {
            RaidTemplateCount current = Get(templateId);
            int owned = Math.Max(0, current.Owned + ownedDelta);
            int fir = Math.Min(owned, Math.Max(0, current.FoundInRaid + firDelta));
            if (owned == 0) totals.Remove(templateId);
            else totals[templateId] = new RaidTemplateCount(owned, fir);
        }

        static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        sealed class RaidItemRecord
        {
            public RaidItemRecord(string templateId, int stackCount, bool foundInRaid)
            { TemplateId = templateId; StackCount = stackCount; FoundInRaid = foundInRaid; }
            public string TemplateId { get; }
            public int StackCount { get; }
            public bool FoundInRaid { get; }
        }
    }

    public sealed class RaidInventoryItemSnapshot
    {
        public RaidInventoryItemSnapshot(string itemId, string templateId, int stackCount, bool foundInRaid)
        {
            ItemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            TemplateId = RequirementContribution.NormalizeId(templateId);
            StackCount = Math.Max(0, stackCount);
            FoundInRaid = foundInRaid;
        }
        public string ItemId { get; }
        public string TemplateId { get; }
        public int StackCount { get; }
        public bool FoundInRaid { get; }
    }

    public sealed class RaidTemplateCount
    {
        internal static readonly RaidTemplateCount Empty = new RaidTemplateCount(0, 0);
        internal RaidTemplateCount(int owned, int foundInRaid)
        { Owned = Math.Max(0, owned); FoundInRaid = Math.Min(Owned, Math.Max(0, foundInRaid)); }
        public int Owned { get; }
        public int FoundInRaid { get; }
    }
}
