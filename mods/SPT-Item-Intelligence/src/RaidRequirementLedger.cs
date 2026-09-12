using System;
using System.Collections.Generic;

namespace SPTItemIntelligence
{
    public sealed class RaidRequirementLedger
    {
        readonly Dictionary<string, RaidItemRecord> items = new Dictionary<string, RaidItemRecord>(StringComparer.Ordinal);
        readonly Dictionary<string, RaidTemplateCount> totals = new Dictionary<string, RaidTemplateCount>(StringComparer.Ordinal);

        public int Revision { get; private set; }
        public int ItemCount => items.Count;

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
            if (items.Count == 0 && totals.Count == 0) return;
            items.Clear();
            totals.Clear();
            Revision++;
        }

        public ItemIntelligenceDecision Evaluate(string templateId, ItemRequirementAllocation baseline, bool candidateFoundInRaid)
        {
            RaidTemplateCount count = Get(templateId);
            return ItemIntelligenceDecisionEngine.Evaluate(baseline, candidateFoundInRaid, count.Owned, count.FoundInRaid);
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

    public sealed class RaidTemplateCount
    {
        internal static readonly RaidTemplateCount Empty = new RaidTemplateCount(0, 0);
        internal RaidTemplateCount(int owned, int foundInRaid)
        { Owned = Math.Max(0, owned); FoundInRaid = Math.Min(Owned, Math.Max(0, foundInRaid)); }
        public int Owned { get; }
        public int FoundInRaid { get; }
    }
}
