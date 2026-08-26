using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerCapabilityDecisionProvider
    {
        private readonly PlannerClientCache cache;
        private readonly object gate = new object();
        private readonly Dictionary<string, CachedDecision> cached = new Dictionary<string, CachedDecision>(StringComparer.Ordinal);
        private long cachedRevision = -1;

        public PlannerCapabilityDecisionProvider(PlannerClientCache cache)
        {
            this.cache = cache ?? throw new ArgumentNullException("cache");
        }

        public bool TryGet(
            PlannerCapabilityGoalDefinition definition,
            out PlannerCapabilityDecisionSnapshot snapshot,
            out string error,
            bool includeAvailable = false)
        {
            snapshot = null;
            error = null;
            if (definition == null)
            {
                error = "Capability goal definition is missing.";
                return false;
            }

            lock (gate)
            {
                if (!cache.HasTopology || !cache.HasState)
                {
                    error = "Quest Planner cached topology/state is not ready.";
                    return false;
                }

                long revision = cache.Revision;
                if (revision != cachedRevision)
                {
                    cachedRevision = revision;
                    cached.Clear();
                }

                string key = BuildKey(definition, includeAvailable);
                CachedDecision existing;
                if (cached.TryGetValue(key, out existing))
                {
                    snapshot = existing.Snapshot;
                    return true;
                }

                try
                {
                    PlannerCapabilityDecisionSnapshot built = Build(definition, includeAvailable);
                    cached[key] = new CachedDecision(built);
                    snapshot = built;
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.GetBaseException().Message;
                    return false;
                }
            }
        }

        private PlannerCapabilityDecisionSnapshot Build(
            PlannerCapabilityGoalDefinition definition,
            bool includeAvailable)
        {
            PlannerTopologyIndex topology = cache.TopologyIndex;
            PlannerLocationIndex locations = cache.LocationIndex;
            PlannerClientIndex state = cache.Index;
            PlannerPayload statePayload = cache.State;

            PlannerCapabilityGoal goal = PlannerCapabilityGoalBuilder.Build(definition, topology, state);
            PlannerClientDelayIndex delays = PlannerClientDelayIndexBuilder.Build(statePayload.Json);
            PlannerRaidFocusDelayEvidence delayEvidence = PlannerRaidFocusDelayEvidenceBuilder.Build(goal.QuestIntent, delays);

            PlannerRaidDecisionPresentation raidPresentation = null;
            if (goal.HasActionableQuestWork)
            {
                IReadOnlyList<PlannerRaidOpportunity> opportunities = PlannerRaidOpportunityBuilder.Build(
                    locations,
                    state,
                    includeAvailable,
                    64,
                    128);

                List<PlannerRaidDecisionCandidate> candidates = new List<PlannerRaidDecisionCandidate>();
                for (int i = 0; i < opportunities.Count; i++)
                {
                    PlannerRaidPlan plan = PlannerRaidPlanBuilder.Build(opportunities[i], state);
                    PlannerRaidDecisionSignals signals = PlannerRaidDecisionSignalBuilder.Build(plan, topology, state);
                    candidates.Add(new PlannerRaidDecisionCandidate(plan.LocationId, signals));
                }

                PlannerRaidDecisionSet set = PlannerRaidDecisionSetBuilder.Build(candidates, goal.QuestIntent);
                raidPresentation = PlannerRaidDecisionPresentationBuilder.Build(set);
            }

            PlannerCapabilityGoalPresentation presentation = PlannerCapabilityGoalPresentationBuilder.Build(
                goal,
                raidPresentation,
                delayEvidence);
            return PlannerCapabilityDecisionSnapshotBuilder.Build(presentation);
        }

        private static string BuildKey(PlannerCapabilityGoalDefinition definition, bool includeAvailable)
        {
            return definition.CapabilityId + "\u001f" +
                   definition.GateQuestId + "\u001f" +
                   ((int)definition.SupplyKind).ToString() + "\u001f" +
                   (definition.ItemTemplateId ?? string.Empty) + "\u001f" +
                   (definition.MaxUnitsPerReset.HasValue ? definition.MaxUnitsPerReset.Value.ToString() : string.Empty) + "\u001f" +
                   (definition.MaxAcquisitionsPerReset.HasValue ? definition.MaxAcquisitionsPerReset.Value.ToString() : string.Empty) + "\u001f" +
                   (includeAvailable ? "1" : "0");
        }

        private sealed class CachedDecision
        {
            public CachedDecision(PlannerCapabilityDecisionSnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public PlannerCapabilityDecisionSnapshot Snapshot { get; private set; }
        }
    }
}
