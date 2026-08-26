using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerCapabilityDecisionProvider
    {
        private const int MaxCaptureAttempts = 3;

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
                CacheCapture captured;
                if (!TryCapture(out captured, out error)) return false;

                if (captured.Revision != cachedRevision)
                {
                    cachedRevision = captured.Revision;
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
                    PlannerCapabilityDecisionSnapshot built = Build(definition, includeAvailable, captured);
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

        private bool TryCapture(out CacheCapture captured, out string error)
        {
            captured = null;
            error = null;

            for (int attempt = 0; attempt < MaxCaptureAttempts; attempt++)
            {
                long before = cache.Revision;
                PlannerTopologyIndex topology = cache.TopologyIndex;
                PlannerLocationIndex locations = cache.LocationIndex;
                PlannerPayload statePayload = cache.State;
                PlannerClientIndex state = cache.Index;
                long after = cache.Revision;

                if (before != after) continue;
                if (topology == null || locations == null || statePayload == null || state == null)
                {
                    error = "Quest Planner cached topology/state is not ready.";
                    return false;
                }

                captured = new CacheCapture(before, topology, locations, statePayload, state);
                return true;
            }

            error = "Quest Planner cache changed while building a capability decision; retry after refresh completes.";
            return false;
        }

        private static PlannerCapabilityDecisionSnapshot Build(
            PlannerCapabilityGoalDefinition definition,
            bool includeAvailable,
            CacheCapture captured)
        {
            PlannerCapabilityGoal goal = PlannerCapabilityGoalBuilder.Build(
                definition,
                captured.Topology,
                captured.State);
            PlannerClientDelayIndex delays = PlannerClientDelayIndexBuilder.Build(captured.StatePayload.Json);
            PlannerRaidFocusDelayEvidence delayEvidence = PlannerRaidFocusDelayEvidenceBuilder.Build(goal.QuestIntent, delays);

            PlannerRaidDecisionPresentation raidPresentation = null;
            if (goal.HasActionableQuestWork)
            {
                IReadOnlyList<PlannerRaidOpportunity> opportunities = PlannerRaidOpportunityBuilder.Build(
                    captured.Locations,
                    captured.State,
                    includeAvailable,
                    64,
                    128);

                List<PlannerRaidDecisionCandidate> candidates = new List<PlannerRaidDecisionCandidate>();
                for (int i = 0; i < opportunities.Count; i++)
                {
                    PlannerRaidPlan plan = PlannerRaidPlanBuilder.Build(opportunities[i], captured.State);
                    PlannerRaidDecisionSignals signals = PlannerRaidDecisionSignalBuilder.Build(
                        plan,
                        captured.Topology,
                        captured.State);
                    candidates.Add(new PlannerRaidDecisionCandidate(plan.LocationId, signals));
                }

                PlannerRaidDecisionSet set = PlannerRaidDecisionSetBuilder.Build(candidates, goal.QuestIntent);
                raidPresentation = PlannerRaidDecisionPresentationBuilder.Build(set);
            }

            PlannerCapabilityGoalPresentation presentation = PlannerCapabilityGoalPresentationBuilder.Build(
                goal,
                raidPresentation,
                delayEvidence);
            return PlannerCapabilityDecisionSnapshotBuilder.Build(
                presentation,
                captured.Revision,
                captured.State.GeneratedAtUnixSeconds);
        }

        private static string BuildKey(PlannerCapabilityGoalDefinition definition, bool includeAvailable)
        {
            return definition.CapabilityId + "\u001f" +
                   definition.GateQuestId + "\u001f" +
                   definition.Owner + "\u001f" +
                   ((int)definition.SupplyKind).ToString() + "\u001f" +
                   (definition.ItemTemplateId ?? string.Empty) + "\u001f" +
                   (definition.MaxUnitsPerReset.HasValue ? definition.MaxUnitsPerReset.Value.ToString() : string.Empty) + "\u001f" +
                   (definition.MaxAcquisitionsPerReset.HasValue ? definition.MaxAcquisitionsPerReset.Value.ToString() : string.Empty) + "\u001f" +
                   (definition.EvidenceSource ?? string.Empty) + "\u001f" +
                   (includeAvailable ? "1" : "0");
        }

        private sealed class CacheCapture
        {
            public CacheCapture(
                long revision,
                PlannerTopologyIndex topology,
                PlannerLocationIndex locations,
                PlannerPayload statePayload,
                PlannerClientIndex state)
            {
                Revision = revision;
                Topology = topology;
                Locations = locations;
                StatePayload = statePayload;
                State = state;
            }

            public long Revision { get; private set; }
            public PlannerTopologyIndex Topology { get; private set; }
            public PlannerLocationIndex Locations { get; private set; }
            public PlannerPayload StatePayload { get; private set; }
            public PlannerClientIndex State { get; private set; }
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
