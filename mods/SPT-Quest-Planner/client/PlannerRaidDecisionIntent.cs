using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidDecisionIntent
    {
        public PlannerRaidDecisionIntent(
            string focusQuestId = null,
            IReadOnlyList<string> focusPathQuestIds = null)
        {
            FocusQuestId = focusQuestId == null ? string.Empty : focusQuestId.Trim();
            FocusPathQuestIds = Normalize(focusPathQuestIds);
        }

        public string FocusQuestId { get; private set; }
        public IReadOnlyList<string> FocusPathQuestIds { get; private set; }
        public bool HasFocusQuest { get { return !string.IsNullOrWhiteSpace(FocusQuestId); } }
        public bool HasFocusPath { get { return FocusPathQuestIds.Count > 0; } }

        private static IReadOnlyList<string> Normalize(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return Array.Empty<string>();
            string[] result = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return result;
        }
    }

    public static class PlannerRaidDecisionIntentBuilder
    {
        public static PlannerRaidDecisionIntent Build(
            string focusQuestId,
            PlannerTopologyIndex topology,
            PlannerClientIndex state)
        {
            if (topology == null) throw new ArgumentNullException("topology");
            if (state == null) throw new ArgumentNullException("state");
            if (string.IsNullOrWhiteSpace(focusQuestId)) return new PlannerRaidDecisionIntent();

            string normalized = focusQuestId.Trim();
            PlannerQueryEngine query = new PlannerQueryEngine(topology, state);
            IReadOnlyList<string> path = query.GetIncompletePrerequisitePlan(normalized);
            return new PlannerRaidDecisionIntent(normalized, path);
        }
    }

    public static class PlannerRaidDecisionIntentPolicy
    {
        public static PlannerRaidDecision Decide(
            PlannerRaidDecisionSignals left,
            PlannerRaidDecisionSignals right,
            PlannerRaidDecisionIntent intent)
        {
            if (left == null) throw new ArgumentNullException("left");
            if (right == null) throw new ArgumentNullException("right");

            if (intent != null && intent.HasFocusQuest)
            {
                string leftMatch;
                string rightMatch;
                bool leftSupports = TryGetFocusMatch(left, intent, out leftMatch);
                bool rightSupports = TryGetFocusMatch(right, intent, out rightMatch);
                if (leftSupports != rightSupports)
                {
                    string matchedQuestId = leftSupports ? leftMatch : rightMatch;
                    return new PlannerRaidDecision(
                        leftSupports ? PlannerRaidDecisionOutcome.PreferLeft : PlannerRaidDecisionOutcome.PreferRight,
                        intent.HasFocusPath
                            ? "Player progression focus selects a candidate that advances the focused quest path."
                            : "Player progression focus explicitly selects a candidate that advances the focused quest.",
                        new[]
                        {
                            (leftSupports ? "LEFT" : "RIGHT") + ": advances " + matchedQuestId +
                            (intent.HasFocusPath ? " on the prerequisite path to " + intent.FocusQuestId : string.Empty)
                        });
                }
            }

            return PlannerRaidDecisionPolicy.Decide(left, right);
        }

        public static bool Supports(PlannerRaidDecisionSignals signals, PlannerRaidDecisionIntent intent)
        {
            string ignored;
            return TryGetFocusMatch(signals, intent, out ignored);
        }

        private static bool TryGetFocusMatch(
            PlannerRaidDecisionSignals signals,
            PlannerRaidDecisionIntent intent,
            out string matchedQuestId)
        {
            matchedQuestId = string.Empty;
            if (signals == null || intent == null || !intent.HasFocusQuest) return false;

            if (intent.HasFocusPath)
            {
                for (int p = 0; p < intent.FocusPathQuestIds.Count; p++)
                {
                    string pathQuestId = intent.FocusPathQuestIds[p];
                    if (!ContainsQuest(signals, pathQuestId)) continue;
                    matchedQuestId = pathQuestId;
                    return true;
                }
                return false;
            }

            if (!ContainsQuest(signals, intent.FocusQuestId)) return false;
            matchedQuestId = intent.FocusQuestId;
            return true;
        }

        private static bool ContainsQuest(PlannerRaidDecisionSignals signals, string questId)
        {
            for (int i = 0; i < signals.NonRepeatableQuestIds.Count; i++)
                if (string.Equals(signals.NonRepeatableQuestIds[i], questId, StringComparison.Ordinal)) return true;
            for (int i = 0; i < signals.RepeatableQuestIds.Count; i++)
                if (string.Equals(signals.RepeatableQuestIds[i], questId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
