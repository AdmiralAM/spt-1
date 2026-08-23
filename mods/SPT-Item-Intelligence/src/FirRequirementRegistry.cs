using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace SPTItemIntelligence
{
    public sealed class FirRequirementState
    {
        internal static readonly FirRequirementState Empty = new FirRequirementState(0, 0, 0);

        public FirRequirementState(int ownedFoundInRaid, int questNowFoundInRaid, int questLaterFoundInRaid)
        {
            OwnedFoundInRaid = Math.Max(0, ownedFoundInRaid);
            QuestNowFoundInRaid = Math.Max(0, questNowFoundInRaid);
            QuestLaterFoundInRaid = Math.Max(0, questLaterFoundInRaid);
        }

        public int OwnedFoundInRaid { get; }
        public int QuestNowFoundInRaid { get; }
        public int QuestLaterFoundInRaid { get; }
    }

    public static class FirRequirementRegistry
    {
        static IReadOnlyDictionary<string, FirRequirementState> current =
            new ReadOnlyDictionary<string, FirRequirementState>(new Dictionary<string, FirRequirementState>(StringComparer.Ordinal));

        public static FirRequirementState Get(string templateId)
        {
            string normalized = RequirementContribution.NormalizeId(templateId);
            if (normalized.Length == 0) return FirRequirementState.Empty;
            FirRequirementState state;
            return Volatile.Read(ref current).TryGetValue(normalized, out state) ? state : FirRequirementState.Empty;
        }

        public static void Publish(IDictionary<string, FirRequirementState> states)
        {
            Dictionary<string, FirRequirementState> copy = new Dictionary<string, FirRequirementState>(StringComparer.Ordinal);
            if (states != null)
            {
                foreach (KeyValuePair<string, FirRequirementState> pair in states)
                {
                    string normalized = RequirementContribution.NormalizeId(pair.Key);
                    if (normalized.Length == 0 || pair.Value == null) continue;
                    copy[normalized] = pair.Value;
                }
            }
            Interlocked.Exchange(ref current, new ReadOnlyDictionary<string, FirRequirementState>(copy));
        }

        internal static void Clear()
        {
            Publish(null);
        }
    }
}
