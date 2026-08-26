using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

            PlannerClientCache cache = ReadyCache(fieldwork, setup, gate);
            PlannerCapabilityGoalDefinition definition = Definition(gate);

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
        public void MultipleGoalsOnSameRevisionReuseSharedDelayAndRaidDerivations()
        {
            const string fieldwork = "fieldwork";
            const string setup = "setup";
            const string gate = "gate";

            PlannerClientCache cache = ReadyCache(fieldwork, setup, gate);
            PlannerCapabilityDecisionProvider provider = new PlannerCapabilityDecisionProvider(cache);
            PlannerCapabilityDecisionSnapshot first;
            PlannerCapabilityDecisionSnapshot second;
            string error;

            Assert.True(provider.TryGet(Definition(gate, "controlled-ammo-a"), out first, out error));
            object firstDelay = PrivateField(provider, "cachedDelayIndex");
            IDictionary firstCandidates = Assert.IsAssignableFrom<IDictionary>(PrivateField(provider, "cachedCandidates"));
            object firstCandidateSet = firstCandidates[false];

            Assert.True(provider.TryGet(Definition(gate, "controlled-ammo-b"), out second, out error));
            object secondDelay = PrivateField(provider, "cachedDelayIndex");
            IDictionary secondCandidates = Assert.IsAssignableFrom<IDictionary>(PrivateField(provider, "cachedCandidates"));
            object secondCandidateSet = secondCandidates[false];

            Assert.NotSame(first, second);
            Assert.Same(firstDelay, secondDelay);
            Assert.Same(firstCandidateSet, secondCandidateSet);
            Assert.Equal(first.SourceRevision, second.SourceRevision);
        }

        [Fact]
        public void ProviderInvalidatesDerivedDecisionWhenCacheRevisionAdvances()
        {
            const string fieldwork = "fieldwork";
            const string setup = "setup";
            const string gate = "gate";

            PlannerClientCache cache = ReadyCache(fieldwork, setup, gate);
            PlannerCapabilityDecisionProvider provider = new PlannerCapabilityDecisionProvider(cache);
            PlannerCapabilityGoalDefinition definition = Definition(gate);

            PlannerCapabilityDecisionSnapshot first;
            string error;
            Assert.True(provider.TryGet(definition, out first, out error));
            long firstRevision = first.SourceRevision;
            object firstDelay = PrivateField(provider, "cachedDelayIndex");
            IDictionary firstCandidates = Assert.IsAssignableFrom<IDictionary>(PrivateField(provider, "cachedCandidates"));
            object firstCandidateSet = firstCandidates[false];
            Assert.Equal(1000, first.GeneratedAtUnixSeconds);

            PlannerClientIndex refreshedState = StateAt(
                2000,
                Active(fieldwork),
                Active(setup),
                Locked(gate),
                Active("reserve-a"),
                Active("reserve-b"),
                Active("reserve-c"));
            cache.ReplaceState(
                new PlannerPayload(
                    PlannerClientContract.SchemaVersion,
                    2000,
                    StateJsonAt(2000, fieldwork, setup, gate, "reserve-a", "reserve-b", "reserve-c")),
                refreshedState);

            PlannerCapabilityDecisionSnapshot second;
            Assert.True(provider.TryGet(definition, out second, out error));
            Assert.True(second.SourceRevision > firstRevision);
            Assert.Equal(cache.Revision, second.SourceRevision);
            Assert.Equal(2000, second.GeneratedAtUnixSeconds);
            Assert.NotSame(first, second);
            Assert.NotSame(firstDelay, PrivateField(provider, "cachedDelayIndex"));
            IDictionary secondCandidates = Assert.IsAssignableFrom<IDictionary>(PrivateField(provider, "cachedCandidates"));
            Assert.NotSame(firstCandidateSet, secondCandidates[false]);
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

        private static object PrivateField(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            object value = field.GetValue(instance);
            Assert.NotNull(value);
            return value;
        }

        private static PlannerClientCache ReadyCache(string fieldwork, string setup, string gate)
        {
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

            PlannerClientIndex state = StateAt(
                1000,
                Active(fieldwork),
                Active(setup),
                Locked(gate),
                Active("reserve-a"),
                Active("reserve-b"),
                Active("reserve-c"));
            cache.ReplaceState(
                new PlannerPayload(
                    PlannerClientContract.SchemaVersion,
                    1000,
                    StateJsonAt(1000, fieldwork, setup, gate, "reserve-a", "reserve-b", "reserve-c")),
                state);
            return cache;
        }

        private static PlannerCapabilityGoalDefinition Definition(string gate, string capabilityId = "controlled-ammo")
        {
            return new PlannerCapabilityGoalDefinition(
                capabilityId,
                gate,
                "test",
                PlannerCapabilitySupplyKind.BoundedRenewable,
                "ammo",
                80,
                80,
                "test-contract");
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

        private static PlannerClientIndex StateAt(long generatedAt, params PlannerQuestClientState[] quests)
        {
            Dictionary<string, PlannerQuestClientState> values = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            foreach (PlannerQuestClientState quest in quests) values[quest.QuestId] = quest;
            return new PlannerClientIndex(generatedAt, values, new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
        }

        private static string StateJsonAt(long generatedAt, params string[] questIds)
        {
            List<string> entries = new List<string>();
            foreach (string id in questIds)
            {
                int raw = id == "gate" ? 0 : 2;
                entries.Add("\"" + id + "\":{\"questId\":\"" + id + "\",\"rawStatus\":" + raw + "}");
            }
            return "{" +
                "\"schemaVersion\":" + PlannerClientContract.SchemaVersion + "," +
                "\"generatedAtUnixSeconds\":" + generatedAt + "," +
                "\"player\":{\"questStates\":{" + string.Join(",", entries) + "}}" +
                "}";
        }
    }
}
