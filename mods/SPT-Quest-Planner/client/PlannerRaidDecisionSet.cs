using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidDecisionCandidate
    {
        public PlannerRaidDecisionCandidate(string locationId, PlannerRaidDecisionSignals signals)
        {
            LocationId = locationId ?? string.Empty;
            Signals = signals ?? throw new ArgumentNullException("signals");
        }

        public string LocationId { get; private set; }
        public PlannerRaidDecisionSignals Signals { get; private set; }
    }

    public sealed class PlannerRaidDecisionSet
    {
        public PlannerRaidDecisionSet(
            PlannerRaidDecisionCandidate recommendation,
            IReadOnlyList<PlannerRaidDecisionCandidate> contenders,
            string reason)
        {
            Recommendation = recommendation;
            Contenders = contenders ?? Array.Empty<PlannerRaidDecisionCandidate>();
            Reason = reason ?? string.Empty;
        }

        public PlannerRaidDecisionCandidate Recommendation { get; private set; }
        public IReadOnlyList<PlannerRaidDecisionCandidate> Contenders { get; private set; }
        public string Reason { get; private set; }
        public bool HasUniqueRecommendation { get { return Recommendation != null; } }
    }

    public static class PlannerRaidDecisionSetBuilder
    {
        private const int MaxCandidates = 64;

        public static PlannerRaidDecisionSet Build(IEnumerable<PlannerRaidDecisionCandidate> candidates)
        {
            return Build(candidates, null);
        }

        public static PlannerRaidDecisionSet Build(
            IEnumerable<PlannerRaidDecisionCandidate> candidates,
            PlannerRaidDecisionIntent intent)
        {
            if (candidates == null) throw new ArgumentNullException("candidates");

            PlannerRaidDecisionCandidate[] source = candidates
                .Where(value => value != null)
                .OrderBy(value => value.LocationId, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (source.Length > MaxCandidates)
                throw new InvalidOperationException("Quest Planner decision set exceeds bounded candidate limit of " + MaxCandidates + ".");
            if (source.Length == 0)
                return new PlannerRaidDecisionSet(null, Array.Empty<PlannerRaidDecisionCandidate>(), "No raid candidates are currently available.");
            if (source.Length == 1)
                return new PlannerRaidDecisionSet(source[0], source, "Only one raid candidate is currently available.");

            List<PlannerRaidDecisionCandidate> frontier = new List<PlannerRaidDecisionCandidate>();
            for (int i = 0; i < source.Length; i++)
            {
                PlannerRaidDecisionCandidate candidate = source[i];
                bool dominated = false;
                for (int n = 0; n < source.Length; n++)
                {
                    if (i == n) continue;
                    PlannerRaidDecision decision = PlannerRaidDecisionIntentPolicy.Decide(
                        source[n].Signals,
                        candidate.Signals,
                        intent);
                    if (decision.Outcome == PlannerRaidDecisionOutcome.PreferLeft)
                    {
                        dominated = true;
                        break;
                    }
                }
                if (!dominated) frontier.Add(candidate);
            }

            frontier.Sort((a, b) => string.Compare(a.LocationId, b.LocationId, StringComparison.OrdinalIgnoreCase));

            if (frontier.Count == 1)
            {
                string reason = intent != null && PlannerRaidDecisionIntentPolicy.Supports(frontier[0].Signals, intent)
                    ? "Player progression focus resolves the candidate frontier toward a raid that advances the focused quest path."
                    : "One candidate is undominated by every alternative under the proven decision dimensions.";
                return new PlannerRaidDecisionSet(frontier[0], frontier.ToArray(), reason);
            }

            string frontierReason = intent != null && intent.HasFocusQuest
                ? frontier.Count + " candidates still advance or remain compatible with the progression focus; expose their trade-offs instead of forcing a best raid."
                : frontier.Count + " candidates remain undominated; expose their trade-offs instead of forcing a best raid.";
            return new PlannerRaidDecisionSet(null, frontier.ToArray(), frontierReason);
        }
    }
}
