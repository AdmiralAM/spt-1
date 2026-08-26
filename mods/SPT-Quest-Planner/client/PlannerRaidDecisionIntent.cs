using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidDecisionIntent
    {
        public PlannerRaidDecisionIntent(
            string focusQuestId = null,
            IReadOnlyList<string> focusPathQuestIds = null,
            IReadOnlyList<string> focusFrontierQuestIds = null,
            IReadOnlyList<string> focusActionableQuestIds = null,
            IReadOnlyList<string> focusEligibilityUnknownQuestIds = null,
            IReadOnlyList<PlannerTopologyPrerequisite> focusTerminalConflictEdges = null)
        {
            FocusQuestId = focusQuestId == null ? string.Empty : focusQuestId.Trim();
            FocusPathQuestIds = Normalize(focusPathQuestIds);
            FocusFrontierQuestIds = Normalize(focusFrontierQuestIds);
            FocusActionableQuestIds = Normalize(focusActionableQuestIds);
            FocusEligibilityUnknownQuestIds = Normalize(focusEligibilityUnknownQuestIds);
            FocusTerminalConflictEdges = NormalizeEdges(focusTerminalConflictEdges);
        }

        public string FocusQuestId { get; private set; }
        public IReadOnlyList<string> FocusPathQuestIds { get; private set; }
        // Quest-prerequisite frontier: no incomplete quest prerequisite remains. This does not by itself prove availability.
        public IReadOnlyList<string> FocusFrontierQuestIds { get; private set; }
        // Actionable frontier: prerequisite-ready plus authoritative profile evaluation says Active or Available.
        public IReadOnlyList<string> FocusActionableQuestIds { get; private set; }
        // Prerequisite-ready nodes for which profile eligibility is not present in the current state snapshot.
        public IReadOnlyList<string> FocusEligibilityUnknownQuestIds { get; private set; }
        // Non-repeatable source is already terminal Success, but the target edge does not accept Success.
        public IReadOnlyList<PlannerTopologyPrerequisite> FocusTerminalConflictEdges { get; private set; }
        public bool HasFocusQuest { get { return !string.IsNullOrWhiteSpace(FocusQuestId); } }
        public bool HasFocusPath { get { return FocusPathQuestIds.Count > 0; } }
        public bool HasFocusFrontier { get { return FocusFrontierQuestIds.Count > 0; } }
        public bool HasActionableFocusFrontier { get { return FocusActionableQuestIds.Count > 0; } }
        public bool HasUnknownFocusEligibility { get { return FocusEligibilityUnknownQuestIds.Count > 0; } }
        public bool HasTerminalFocusConflict { get { return FocusTerminalConflictEdges.Count > 0; } }

        private static IReadOnlyList<string> Normalize(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return Array.Empty<string>();
            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<PlannerTopologyPrerequisite> NormalizeEdges(IReadOnlyList<PlannerTopologyPrerequisite> values)
        {
            if (values == null || values.Count == 0) return Array.Empty<PlannerTopologyPrerequisite>();
            Dictionary<string, PlannerTopologyPrerequisite> unique = new Dictionary<string, PlannerTopologyPrerequisite>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                PlannerTopologyPrerequisite edge = values[i];
                if (edge == null) continue;
                string key = edge.SourceQuestId + "\u001f" + edge.TargetQuestId;
                if (!unique.ContainsKey(key)) unique[key] = edge;
            }
            return unique.Values
                .OrderBy(value => value.SourceQuestId, StringComparer.Ordinal)
                .ThenBy(value => value.TargetQuestId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static class PlannerRaidDecisionIntentBuilder
    {
        private const int AvailableDisposition = 3;
        private const int ActiveDisposition = 4;

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
            List<string> frontier = new List<string>();
            List<string> actionable = new List<string>();
            List<string> eligibilityUnknown = new List<string>();
            List<PlannerTopologyPrerequisite> terminalConflicts = new List<PlannerTopologyPrerequisite>();

            for (int i = 0; i < path.Count; i++)
            {
                string questId = path[i];
                // Missing topology cannot be claimed as a real frontier quest.
                if (topology.GetQuest(questId) == null) continue;

                IReadOnlyList<PlannerTopologyPrerequisite> conflicts = query.GetTerminalPrerequisiteConflicts(questId);
                for (int c = 0; c < conflicts.Count; c++) terminalConflicts.Add(conflicts[c]);

                if (query.GetImmediateBlockers(questId).Count != 0) continue;

                frontier.Add(questId);
                PlannerQuestClientState questState = state.GetQuest(questId);
                if (questState == null)
                {
                    eligibilityUnknown.Add(questId);
                    continue;
                }

                if (questState.Disposition == ActiveDisposition || questState.Disposition == AvailableDisposition)
                    actionable.Add(questId);
            }

            return new PlannerRaidDecisionIntent(
                normalized,
                path,
                frontier.ToArray(),
                actionable.ToArray(),
                eligibilityUnknown.ToArray(),
                terminalConflicts.ToArray());
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
            return PlannerRaidFocusedDecisionPolicy.Decide(left, right, intent);
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
            if (intent.HasTerminalFocusConflict) return false;

            if (intent.HasFocusPath)
            {
                // A future/blocked goal may override the conservative policy only with authoritative
                // profile evidence that a prerequisite-ready path quest is actually active/available.
                if (!intent.HasActionableFocusFrontier) return false;
                for (int p = 0; p < intent.FocusActionableQuestIds.Count; p++)
                {
                    string questId = intent.FocusActionableQuestIds[p];
                    if (!ContainsQuest(signals, questId)) continue;
                    matchedQuestId = questId;
                    return true;
                }
                return false;
            }

            // Direct manually supplied focus remains supported for the already-established active-focus workflow.
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
