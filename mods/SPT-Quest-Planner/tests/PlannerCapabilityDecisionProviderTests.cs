using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCapabilityDecisionProviderTests
    {
        [Fact]
        public void CachedProviderBuildsFocusedSnapshotWithoutLegacyMapRanking()
        {
            const string fieldwork = "fieldwork";
            const string setup = "setup";
            const string gate = "gate";

            PlannerClientCache cache = new PlannerClientCache();
            PlannerTopologyIndex topology = Topology(
                Quest(fieldwork, Array.Empty<string>(), new[] { gate }),
                Quest(setup, Array.Empty<string>(), Array.Empty<string>()),
                Quest(gate, new[] { fieldwork }, Array.Empty<string>()),
                Quest("reserve-a", Array.Empty<string>(), Array.Empty<string>()),
                Quest("reserve-b", Array.Empty<string>(), Array.Empty<string>()),
                Quest("reserve-c", Array.Empty<string>(), Array.Empty<string>()));

            PlannerLocationIndex locations = Locations(
                Bucket("customs",
                    Kill(fieldwork, "fw-kill", "pmc", "customs"),
                    Kill(setup, "setup-kill", "pmc", "customs")),
                Bucket("reserve",
                    Visit("reserve-a", "ra", "reserve"),
                    Visit("reserve-b", "rb", "reserve"),
                    Visit("reserve-c", "rc", "reserve")));

            cache.ReplaceTopology(
                new PlannerPayload(PlannerClientContract.SchemaVersion, 0, "{}"),
                topology,
                new PlannerRequirementIndex(new Dictionary<string, IReadOnlyList<PlannerQuestItemRequirement>>(StringComparer.Ordinal)),
                locations);

            PlannerClientIndex state = State(
                Active(fieldwork),
                Active(setup),
                Locked(gate),
                Active("reserve-a"),
                Active("reserve-b"),
                Active("reserve-c"));
            string stateJson = StateJson(fieldwork, setup, gate, "reserve-a", "reserve-b", "reserve-c");
            cache.ReplaceState(
                new PlannerPayload(PlannerClientContract.SchemaVersion, 1000, stateJson),
                state);

            PlannerCapabilityGoalDefinition definition = new PlannerCapabilityGoalDefinition(
                "controlled-ammo",
                gate,
                "test",
                PlannerCapabilitySupplyKind.BoundedRenewable,
                "ammo",
                80,
                80,
                "test-contract");

            PlannerCapabilityDecisionProvider provider = new PlannerCapabilityDecisionProvider(cache);
            PlannerCapabilityDecisionSnapshot snapshot;
            string error;

            Assert.True(provider.TryGet(definition, out snapshot, out error));
            Assert.Null(error);
            Assert.NotNull(snapshot);
            Assert.Equal("customs", snapshot.PrimaryLocationId);
            Assert.Equal(PlannerCapabilityDecisionValueKind.DecisionChanged, snapshot.DecisionValue);
            Assert.True(snapshot.CountsTowardKeepCandidate);
            Assert.DoesNotContain("reserve", snapshot.AlternativeLocationIds);
            Assert.True(snapshot.HasFreshnessProvenance);
            Assert.Equal(cache.Revision, snapshot.SourceRevision);
            Assert.Equal(1000, snapshot.GeneratedAtUnixSeconds);
        }

        [Fact]
        public void ProviderFailsCleanlyBeforeCachedStateIsReady()
        {
            PlannerClientCache cache = new PlannerClientCache();
            PlannerCapabilityDecisionProvider provider = new PlannerCapabilityDecisionProvider(cache);
            PlannerCapabilityGoalDefinition definition = new PlannerCapabilityGoalDefinition(
                "capability",
                "gate",
                "test",
                PlannerCapabilitySupplyKind.OneTimeSample);

            PlannerCapabilityDecisionSnapshot snapshot;
            string error;
            Assert.False(provider.TryGet(definition, out snapshot, out error));
            Assert.Null(snapshot);
            Assert.Contains("not ready", error, StringComparison.OrdinalIgnoreCase);
        }

        private static PlannerTopologyQuest Quest(string id, IReadOnlyList<string> prerequisites, IReadOnlyList<string> dependents)
        {
            return new PlannerTopologyQuest(
                id,
                "trader",
                id,
                null,
                false,
                prerequisites,
                dependents,
                Array.Empty<string>());
        }

        private static PlannerTopologyIndex Topology(params PlannerTopologyQuest[] quests)
        {
            Dictionary<string, PlannerTopologyQuest> values = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
            foreach (PlannerTopologyQuest quest in quests) values[quest.QuestId] = quest;
            return new PlannerTopologyIndex(values, new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
        }

        private static PlannerLocationIndex Locations(params PlannerLocationBucket[] buckets)
        {
            Dictionary<string, PlannerLocationBucket> values = new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase);
            foreach (PlannerLocationBucket bucket in buckets) values[bucket.LocationId] = bucket;
            return new PlannerLocationIndex(values, Array.Empty<PlannerLocationObjective>());
        }

        private static PlannerLocationBucket Bucket(string location, params PlannerLocationObjective[] objectives)
        {
            return new PlannerLocationBucket(location, objectives);
        }

        private static PlannerLocationObjective Kill(string questId, string conditionId, string target, string location)
        {
            return new PlannerLocationObjective(
                questId,
                conditionId,
                "Kill",
                "Finish",
                null,
                new[] { target },
                new[] { location },
                PlannerObjectiveKind.Kill,
                1);
        }

        private static PlannerLocationObjective Visit(string questId, string conditionId, string location)
        {
            return new PlannerLocationObjective(
                questId,
                conditionId,
                "VisitPlace",
                "Finish",
                null,
                Array.Empty<string>(),
                new[] { location },
                PlannerObjectiveKind.Visit,
                1);
        }

        private static PlannerQuestClientState Active(string questId)
        {
            return new PlannerQuestClientState(questId, 4, 3, true, true, 2);
        }

        private static PlannerQuestClientState Locked(string questId)
        {
            return new PlannerQuestClientState(questId, 1, 1, true, false, 0);
        }

        private static PlannerClientIndex State(params PlannerQuestClientState[] quests)
        {
            Dictionary<string, PlannerQuestClientState> values = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            foreach (PlannerQuestClientState quest in quests) values[quest.QuestId] = quest;
            return new PlannerClientIndex(1000, values, new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
        }

        private static string StateJson(params string[] questIds)
        {
            List<string> entries = new List<string>();
            foreach (string id in questIds)
            {
                int raw = id == "gate" ? 0 : 2;
                entries.Add("\"" + id + "\":{\"questId\":\"" + id + "\",\"rawStatus\":" + raw + "}");
            }
            return "{" +
                "\"schemaVersion\":" + PlannerClientContract.SchemaVersion + "," +
                "\"generatedAtUnixSeconds\":1000," +
                "\"player\":{\"questStates\":{" + string.Join(",", entries) + "}}" +
                "}";
        }
    }
}
