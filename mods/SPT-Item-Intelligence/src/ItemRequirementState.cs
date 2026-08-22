using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace SPTItemIntelligence
{
    public enum ItemRequirementDecision
    {
        None,
        Keep,
        SafeToSell
    }

    public sealed class ItemRequirementState
    {
        internal static readonly ItemRequirementState Empty = new ItemRequirementState(
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            0,
            RequirementReasonFlags.None,
            ItemRequirementDecision.None,
            string.Empty);

        internal ItemRequirementState(
            string templateId,
            int ownedCount,
            int questNeededNow,
            int questNeededLater,
            int hideoutNeeded,
            int keepCount,
            int surplusCount,
            RequirementReasonFlags reasons,
            ItemRequirementDecision decision,
            string holdReason)
        {
            TemplateId = templateId ?? string.Empty;
            OwnedCount = Math.Max(0, ownedCount);
            QuestNeededNow = Math.Max(0, questNeededNow);
            QuestNeededLater = Math.Max(0, questNeededLater);
            HideoutNeeded = Math.Max(0, hideoutNeeded);
            KeepCount = Math.Max(0, keepCount);
            SurplusCount = Math.Max(0, surplusCount);
            Reasons = reasons;
            Decision = decision;
            HoldReason = holdReason ?? string.Empty;
        }

        public string TemplateId { get; }
        public int OwnedCount { get; }
        public int QuestNeededNow { get; }
        public int QuestNeededLater { get; }
        public int HideoutNeeded { get; }
        public int KeepCount { get; }
        public int SurplusCount { get; }
        public RequirementReasonFlags Reasons { get; }
        public ItemRequirementDecision Decision { get; }
        public string HoldReason { get; }
        public bool RequiresFoundInRaid => (Reasons & RequirementReasonFlags.FoundInRaid) != 0;
        public bool HasRequirement => KeepCount > 0;
        public bool IsSafeToSell => Decision == ItemRequirementDecision.SafeToSell;
    }

    public sealed class ItemRequirementStateIndex
    {
        static readonly ItemRequirementStateIndex empty = new ItemRequirementStateIndex(
            0,
            new Dictionary<string, ItemRequirementState>(StringComparer.Ordinal));

        readonly ReadOnlyDictionary<string, ItemRequirementState> entries;

        internal ItemRequirementStateIndex(long generatedAtUnixSeconds, IDictionary<string, ItemRequirementState> entries)
        {
            GeneratedAtUnixSeconds = Math.Max(0, generatedAtUnixSeconds);
            this.entries = new ReadOnlyDictionary<string, ItemRequirementState>(
                new Dictionary<string, ItemRequirementState>(entries, StringComparer.Ordinal));
        }

        public static ItemRequirementStateIndex Empty => empty;
        public long GeneratedAtUnixSeconds { get; }
        public int Count => entries.Count;
        public IReadOnlyDictionary<string, ItemRequirementState> Entries => entries;

        public ItemRequirementState Get(string templateId)
        {
            return GetNormalized(RequirementContribution.NormalizeId(templateId));
        }

        public ItemRequirementState GetNormalized(string normalizedTemplateId)
        {
            if (string.IsNullOrEmpty(normalizedTemplateId)) return ItemRequirementState.Empty;
            ItemRequirementState state;
            return entries.TryGetValue(normalizedTemplateId, out state) ? state : ItemRequirementState.Empty;
        }
    }

    public static class ItemRequirementStateBuilder
    {
        public static ItemRequirementStateIndex Build(RequirementIndex index)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));

            Dictionary<string, ItemRequirementState> states = new Dictionary<string, ItemRequirementState>(index.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, RequirementIndexEntry> pair in index.Entries)
            {
                RequirementIndexEntry entry = pair.Value;
                if (entry == null) continue;

                ItemRequirementDecision decision;
                if (entry.SurplusCount > 0) decision = ItemRequirementDecision.SafeToSell;
                else if (entry.KeepCount > 0) decision = ItemRequirementDecision.Keep;
                else decision = ItemRequirementDecision.None;

                states[pair.Key] = new ItemRequirementState(
                    entry.TemplateId,
                    entry.OwnedCount,
                    entry.QuestNeededNow,
                    entry.QuestNeededLater,
                    entry.HideoutNeeded,
                    entry.KeepCount,
                    entry.SurplusCount,
                    entry.Reasons,
                    decision,
                    BuildHoldReason(entry));
            }

            return new ItemRequirementStateIndex(index.GeneratedAtUnixSeconds, states);
        }

        static string BuildHoldReason(RequirementIndexEntry entry)
        {
            if (entry == null || entry.KeepCount <= 0) return string.Empty;

            bool current = (entry.Reasons & RequirementReasonFlags.CurrentQuest) != 0;
            bool future = (entry.Reasons & RequirementReasonFlags.FutureQuest) != 0;
            bool hideout = (entry.Reasons & RequirementReasonFlags.Hideout) != 0;
            bool fir = (entry.Reasons & RequirementReasonFlags.FoundInRaid) != 0;

            if (current && fir) return "Current quest (FIR)";
            if (current) return "Current quest";
            if (hideout) return "Hideout";
            if (future && fir) return "Future quest (FIR)";
            if (future) return "Future quest";
            return "Requirement";
        }
    }

    public sealed class ItemRequirementStateStore
    {
        ItemRequirementStateIndex current = ItemRequirementStateIndex.Empty;

        public ItemRequirementStateIndex Current => Volatile.Read(ref current);

        public void Refresh(RequirementIndex index)
        {
            ItemRequirementStateIndex replacement = ItemRequirementStateBuilder.Build(index ?? RequirementIndex.Empty);
            Interlocked.Exchange(ref current, replacement);
        }
    }
}
