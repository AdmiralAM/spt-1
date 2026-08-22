using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace SPTItemIntelligence
{
    public enum RequirementSource
    {
        CurrentQuest,
        FutureQuest,
        Hideout
    }

    public enum RequirementCombineMode
    {
        Additive,
        AlternativeMaximum
    }

    [Flags]
    public enum RequirementReasonFlags
    {
        None = 0,
        CurrentQuest = 1,
        FutureQuest = 2,
        Hideout = 4,
        FoundInRaid = 8
    }

    public sealed class RequirementContribution
    {
        public RequirementContribution(
            string templateId,
            RequirementSource source,
            int requiredCount,
            int satisfiedCount = 0,
            bool foundInRaidRequired = false,
            RequirementCombineMode combineMode = RequirementCombineMode.Additive,
            string alternativeGroup = null)
        {
            TemplateId = NormalizeId(templateId);
            Source = source;
            RequiredCount = Math.Max(0, requiredCount);
            SatisfiedCount = Math.Min(RequiredCount, Math.Max(0, satisfiedCount));
            FoundInRaidRequired = foundInRaidRequired;
            CombineMode = combineMode;
            AlternativeGroup = NormalizeGroup(alternativeGroup);

            if (TemplateId.Length == 0) throw new ArgumentException("A contribution requires a template id.", nameof(templateId));
            if (CombineMode == RequirementCombineMode.AlternativeMaximum && AlternativeGroup.Length == 0)
                throw new ArgumentException("Alternative contributions require a stable group id.", nameof(alternativeGroup));
        }

        public string TemplateId { get; }
        public RequirementSource Source { get; }
        public int RequiredCount { get; }
        public int SatisfiedCount { get; }
        public int RemainingCount => RequiredCount - SatisfiedCount;
        public bool FoundInRaidRequired { get; }
        public RequirementCombineMode CombineMode { get; }
        public string AlternativeGroup { get; }

        static string NormalizeGroup(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        internal static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }

    public sealed class OwnedTemplateCount
    {
        public OwnedTemplateCount(string templateId, int count)
        {
            TemplateId = RequirementContribution.NormalizeId(templateId);
            Count = Math.Max(0, count);
            if (TemplateId.Length == 0) throw new ArgumentException("An owned count requires a template id.", nameof(templateId));
        }

        public string TemplateId { get; }
        public int Count { get; }
    }

    public sealed class RequirementProjection
    {
        readonly ReadOnlyCollection<OwnedTemplateCount> owned;
        readonly ReadOnlyCollection<RequirementContribution> contributions;

        public RequirementProjection(
            long generatedAtUnixSeconds,
            IEnumerable<OwnedTemplateCount> owned,
            IEnumerable<RequirementContribution> contributions)
        {
            GeneratedAtUnixSeconds = Math.Max(0, generatedAtUnixSeconds);
            this.owned = Copy(owned).AsReadOnly();
            this.contributions = Copy(contributions).AsReadOnly();
        }

        public long GeneratedAtUnixSeconds { get; }
        public IReadOnlyList<OwnedTemplateCount> Owned => owned;
        public IReadOnlyList<RequirementContribution> Contributions => contributions;

        static List<T> Copy<T>(IEnumerable<T> source) where T : class
        {
            List<T> result = new List<T>();
            if (source != null)
                foreach (T value in source)
                    if (value != null) result.Add(value);
            return result;
        }
    }

    public interface IRequirementDataProjector
    {
        RequirementProjection Project(RequirementDataEnvelope snapshot);
    }

    public sealed class RequirementIndexOptions
    {
        public bool IncludeFutureQuests { get; set; } = true;
        public bool IncludeHideout { get; set; } = true;
    }

    public sealed class RequirementIndexEntry
    {
        internal static readonly RequirementIndexEntry Empty = new RequirementIndexEntry(
            string.Empty, 0, 0, 0, 0, 0, 0, RequirementReasonFlags.None);

        internal RequirementIndexEntry(
            string templateId,
            int questNeededNow,
            int questNeededLater,
            int hideoutNeeded,
            int keepCount,
            int ownedCount,
            int surplusCount,
            RequirementReasonFlags reasons)
        {
            TemplateId = templateId;
            QuestNeededNow = questNeededNow;
            QuestNeededLater = questNeededLater;
            HideoutNeeded = hideoutNeeded;
            KeepCount = keepCount;
            OwnedCount = ownedCount;
            SurplusCount = surplusCount;
            Reasons = reasons;
        }

        public string TemplateId { get; }
        public int QuestNeededNow { get; }
        public int QuestNeededLater { get; }
        public int HideoutNeeded { get; }
        public int KeepCount { get; }
        public int OwnedCount { get; }
        public int SurplusCount { get; }
        public RequirementReasonFlags Reasons { get; }
        public bool RequiresFoundInRaid => (Reasons & RequirementReasonFlags.FoundInRaid) != 0;
        public bool HasRequirement => KeepCount > 0;
    }

    public sealed class RequirementIndex
    {
        static readonly RequirementIndex empty = new RequirementIndex(0, new Dictionary<string, RequirementIndexEntry>(StringComparer.Ordinal));
        readonly ReadOnlyDictionary<string, RequirementIndexEntry> entries;

        internal RequirementIndex(long generatedAtUnixSeconds, IDictionary<string, RequirementIndexEntry> entries)
        {
            GeneratedAtUnixSeconds = Math.Max(0, generatedAtUnixSeconds);
            this.entries = new ReadOnlyDictionary<string, RequirementIndexEntry>(
                new Dictionary<string, RequirementIndexEntry>(entries, StringComparer.Ordinal));
        }

        public static RequirementIndex Empty => empty;
        public long GeneratedAtUnixSeconds { get; }
        public int Count => entries.Count;
        public IReadOnlyDictionary<string, RequirementIndexEntry> Entries => entries;

        public RequirementIndexEntry Get(string templateId)
        {
            return GetNormalized(RequirementContribution.NormalizeId(templateId));
        }

        public RequirementIndexEntry GetNormalized(string normalizedTemplateId)
        {
            if (string.IsNullOrEmpty(normalizedTemplateId)) return RequirementIndexEntry.Empty;
            RequirementIndexEntry entry;
            return entries.TryGetValue(normalizedTemplateId, out entry) ? entry : RequirementIndexEntry.Empty;
        }
    }

    public static class RequirementIndexBuilder
    {
        public static RequirementIndex Build(RequirementProjection projection, RequirementIndexOptions options = null)
        {
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            options = options ?? new RequirementIndexOptions();

            Dictionary<string, EntryAccumulator> accumulators = new Dictionary<string, EntryAccumulator>(StringComparer.Ordinal);
            for (int i = 0; i < projection.Owned.Count; i++)
            {
                OwnedTemplateCount owned = projection.Owned[i];
                EntryAccumulator entry = GetOrCreate(accumulators, owned.TemplateId);
                entry.OwnedCount += owned.Count;
            }

            for (int i = 0; i < projection.Contributions.Count; i++)
            {
                RequirementContribution contribution = projection.Contributions[i];
                int remaining = contribution.RemainingCount;
                if (remaining <= 0 || !Included(contribution.Source, options)) continue;
                GetOrCreate(accumulators, contribution.TemplateId).Add(contribution, remaining);
            }

            Dictionary<string, RequirementIndexEntry> published = new Dictionary<string, RequirementIndexEntry>(accumulators.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, EntryAccumulator> pair in accumulators)
            {
                RequirementIndexEntry entry = pair.Value.Finish(pair.Key);
                if (entry.OwnedCount > 0 || entry.HasRequirement) published.Add(pair.Key, entry);
            }
            return new RequirementIndex(projection.GeneratedAtUnixSeconds, published);
        }

        static bool Included(RequirementSource source, RequirementIndexOptions options)
        {
            if (source == RequirementSource.FutureQuest) return options.IncludeFutureQuests;
            if (source == RequirementSource.Hideout) return options.IncludeHideout;
            return source == RequirementSource.CurrentQuest;
        }

        static EntryAccumulator GetOrCreate(Dictionary<string, EntryAccumulator> entries, string templateId)
        {
            EntryAccumulator entry;
            if (!entries.TryGetValue(templateId, out entry))
            {
                entry = new EntryAccumulator();
                entries.Add(templateId, entry);
            }
            return entry;
        }

        sealed class EntryAccumulator
        {
            readonly Dictionary<string, int> alternativeTotals = new Dictionary<string, int>(StringComparer.Ordinal);
            int additiveTotal;

            public int QuestNeededNow;
            public int QuestNeededLater;
            public int HideoutNeeded;
            public int OwnedCount;
            public RequirementReasonFlags Reasons;

            public void Add(RequirementContribution contribution, int remaining)
            {
                switch (contribution.Source)
                {
                    case RequirementSource.CurrentQuest:
                        QuestNeededNow += remaining;
                        Reasons |= RequirementReasonFlags.CurrentQuest;
                        break;
                    case RequirementSource.FutureQuest:
                        QuestNeededLater += remaining;
                        Reasons |= RequirementReasonFlags.FutureQuest;
                        break;
                    case RequirementSource.Hideout:
                        HideoutNeeded += remaining;
                        Reasons |= RequirementReasonFlags.Hideout;
                        break;
                }
                if (contribution.FoundInRaidRequired) Reasons |= RequirementReasonFlags.FoundInRaid;

                if (contribution.CombineMode == RequirementCombineMode.Additive)
                {
                    additiveTotal += remaining;
                    return;
                }

                int current;
                if (!alternativeTotals.TryGetValue(contribution.AlternativeGroup, out current) || remaining > current)
                    alternativeTotals[contribution.AlternativeGroup] = remaining;
            }

            public RequirementIndexEntry Finish(string templateId)
            {
                int keep = additiveTotal;
                foreach (KeyValuePair<string, int> alternative in alternativeTotals) keep += alternative.Value;
                int surplus = Math.Max(0, OwnedCount - keep);
                return new RequirementIndexEntry(
                    templateId,
                    QuestNeededNow,
                    QuestNeededLater,
                    HideoutNeeded,
                    keep,
                    OwnedCount,
                    surplus,
                    Reasons);
            }
        }
    }

    public sealed class RequirementIndexStore
    {
        RequirementIndex current = RequirementIndex.Empty;

        public RequirementIndex Current => Volatile.Read(ref current);

        public bool TryRefresh(
            RequirementDataEnvelope snapshot,
            IRequirementDataProjector projector,
            RequirementIndexOptions options,
            out string error)
        {
            if (snapshot == null)
            {
                error = "Snapshot is missing.";
                return false;
            }
            if (!snapshot.profileReady)
            {
                error = "Profile is not ready.";
                return false;
            }
            if (projector == null)
            {
                error = "Requirement projector is missing.";
                return false;
            }

            try
            {
                RequirementProjection projection = projector.Project(snapshot);
                if (projection == null)
                {
                    error = "Requirement projection is missing.";
                    return false;
                }
                RequirementIndex replacement = RequirementIndexBuilder.Build(projection, options);
                Interlocked.Exchange(ref current, replacement);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
