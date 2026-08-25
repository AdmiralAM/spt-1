using System;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidDecisionIntent
    {
        public PlannerRaidDecisionIntent(string focusQuestId = null)
        {
            FocusQuestId = focusQuestId == null ? string.Empty : focusQuestId.Trim();
        }

        public string FocusQuestId { get; private set; }
        public bool HasFocusQuest { get { return !string.IsNullOrWhiteSpace(FocusQuestId); } }
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
                bool leftSupports = Supports(left, intent.FocusQuestId);
                bool rightSupports = Supports(right, intent.FocusQuestId);
                if (leftSupports != rightSupports)
                {
                    return new PlannerRaidDecision(
                        leftSupports ? PlannerRaidDecisionOutcome.PreferLeft : PlannerRaidDecisionOutcome.PreferRight,
                        "Player progression focus explicitly selects a candidate that advances the focused quest.",
                        new[] { (leftSupports ? "LEFT" : "RIGHT") + ": advances focused quest " + intent.FocusQuestId });
                }
            }

            return PlannerRaidDecisionPolicy.Decide(left, right);
        }

        private static bool Supports(PlannerRaidDecisionSignals signals, string questId)
        {
            for (int i = 0; i < signals.NonRepeatableQuestIds.Count; i++)
                if (string.Equals(signals.NonRepeatableQuestIds[i], questId, StringComparison.Ordinal)) return true;
            for (int i = 0; i < signals.RepeatableQuestIds.Count; i++)
                if (string.Equals(signals.RepeatableQuestIds[i], questId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
