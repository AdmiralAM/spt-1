using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidFocusEvidence
    {
        public PlannerRaidFocusEvidence(
            IReadOnlyList<string> matchedFrontierQuestIds,
            IReadOnlyList<string> focusedImmediateUnlockQuestIds)
        {
            MatchedFrontierQuestIds = matchedFrontierQuestIds ?? Array.Empty<string>();
            FocusedImmediateUnlockQuestIds = focusedImmediateUnlockQuestIds ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> MatchedFrontierQuestIds { get; private set; }
        public IReadOnlyList<string> FocusedImmediateUnlockQuestIds { get; private set; }
        public bool AdvancesExecutableFocus { get { return MatchedFrontierQuestIds.Count > 0; } }
        public bool HasFocusedImmediateLeverage { get { return FocusedImmediateUnlockQuestIds.Count > 0; } }
    }

    public static class PlannerRaidFocusEvidenceBuilder
    {
        public static PlannerRaidFocusEvidence Build(
            PlannerRaidDecisionSignals signals,
            PlannerRaidDecisionIntent intent)
        {
            if (signals == null) throw new ArgumentNullException("signals");
            if (intent == null || !intent.HasFocusQuest)
                return new PlannerRaidFocusEvidence(Array.Empty<string>(), Array.Empty<string>());

            HashSet<string> candidateQuestIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < signals.NonRepeatableQuestIds.Count; i++)
                candidateQuestIds.Add(signals.NonRepeatableQuestIds[i]);
            for (int i = 0; i < signals.RepeatableQuestIds.Count; i++)
                candidateQuestIds.Add(signals.RepeatableQuestIds[i]);

            IEnumerable<string> actionableFocusIds = intent.HasExecutableFocusFrontier
                ? intent.FocusFrontierQuestIds
                : intent.HasFocusPath
                    ? intent.FocusPathQuestIds
                    : new[] { intent.FocusQuestId };

            string[] matches = actionableFocusIds
                .Where(candidateQuestIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            HashSet<string> focusPathIds = new HashSet<string>(
                intent.HasFocusPath ? intent.FocusPathQuestIds : new[] { intent.FocusQuestId },
                StringComparer.Ordinal);
            string[] focusedUnlocks = signals.ImmediateUnlockQuestIds
                .Where(focusPathIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            return new PlannerRaidFocusEvidence(matches, focusedUnlocks);
        }
    }
}
