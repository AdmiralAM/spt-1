using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidDecisionSignalsTests
    {
        [Fact]
        public void Build_DetectsCrossQuestActionOverlapWithoutUsingRawObjectiveCount()
        {
            PlannerTopologyIndex topology = Topology(
                Quest("q1"),
                Quest("q2"),
                Quest("q3"));
            PlannerClientIndex state = EmptyState();

            PlannerRaidPlan plan = Plan(
                new[] { "q1", "q2", "q3" },
                new[]
                {
                    Objective("q1", "c1", PlannerRaidObjectiveKind.Kill, "customs", "pmc"),
                    Objective("q2", "c2", PlannerRaidObjectiveKind.Kill, "customs", "pmc"),
                    Objective("q3", "c3", PlannerRaidObjectiveKind.Visit, "customs", "dorms")
                });

            PlannerRaidDecisionSignals signals = PlannerRaidDecisionSignalBuilder.Build(plan, topology, state);

            Assert.Equal(3, signals.NonRepeatableQuestCount);
            Assert.Equal(1, signals.CrossQuestOverlapGroupCount);
            Assert.Equal(2, signals.MaxOverlappingQuestCount);
            Assert.Equal(PlannerRaidObjectiveKind.Kill, signals.ActionOverlaps[0].Kind);
            Assert.Equal(new[] { "q1", "q2" }, signals.ActionOverlaps[0].QuestIds);
        }

        [Fact]
        public void Build_DoesNotFabricateOverlapForDifferentTargetsOrUnknownKinds()
        {
            PlannerTopologyIndex topology = Topology(Quest("q1"), Quest("q2"), Quest("q3"));
            PlannerRaidPlan plan = Plan(
                new[] { "q1", "q2", "q3" },
                new[]
                {
                    Objective("q1", "c1", PlannerRaidObjectiveKind.Kill, "customs", "pmc"),
                    Objective("q2", "c2", PlannerRaidObjectiveKind.Kill, "customs", "scav"),
                    Objective("q3", "c3", PlannerRaidObjectiveKind.Other, "customs", "pmc")
                });

            PlannerRaidDecisionSignals signals = PlannerRaidDecisionSignalBuilder.Build(plan, topology, EmptyState());

            Assert.Empty(signals.ActionOverlaps);
            Assert.Equal(1, signals.UnknownObjectiveCount);
            Assert.Equal(2d / 3d, signals.EvidenceCoverage, 6);
        }

        [Fact]
        public void Build_SeparatesRepeatableInflationFromNonRepeatableProgression()
        {
            PlannerTopologyIndex topology = Topology(
                Quest("main"),
                Quest("daily1", repeatable: true),
                Quest("daily2", repeatable: true));
            PlannerRaidPlan plan = Plan(
                new[] { "main", "daily1", "daily2" },
                new[]
                {
                    Objective("main", "m", PlannerRaidObjectiveKind.Extract, "shoreline"),
                    Objective("daily1", "d1", PlannerRaidObjectiveKind.Kill, "shoreline", "scav"),
                    Objective("daily2", "d2", PlannerRaidObjectiveKind.Kill, "shoreline", "scav")
                });

            PlannerRaidDecisionSignals signals = PlannerRaidDecisionSignalBuilder.Build(plan, topology, EmptyState());

            Assert.Equal(1, signals.NonRepeatableQuestCount);
            Assert.Equal(2, signals.RepeatableQuestCount);
            Assert.Equal(2d / 3d, signals.RepeatableShare, 6);
        }

        [Fact]
        public void Build_CountsOnlyImmediateUnlocksWhoseOtherPrerequisitesAreAlreadyComplete()
        {
            PlannerTopologyQuest q1 = new PlannerTopologyQuest(
                "q1", null, null, null, false,
                Array.Empty<string>(), new[] { "next-ready", "next-blocked" }, Array.Empty<string>());
            PlannerTopologyQuest blocker = new PlannerTopologyQuest(
                "blocker", null, null, null, false,
                Array.Empty<string>(), new[] { "next-blocked" }, Array.Empty<string>());
            PlannerTopologyQuest nextReady = new PlannerTopologyQuest(
                "next-ready", null, null, null, false,
                new[] { "q1" }, Array.Empty<string>(), Array.Empty<string>());
            PlannerTopologyQuest nextBlocked = new PlannerTopologyQuest(
                "next-blocked", null, null, null, false,
                new[] { "q1", "blocker" }, Array.Empty<string>(), Array.Empty<string>());

            PlannerTopologyIndex topology = Topology(q1, blocker, nextReady, nextBlocked);
            PlannerClientIndex state = new PlannerClientIndex(
                0,
                new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
                {
                    ["q1"] = new PlannerQuestClientState("q1", 2, 0, true, true),
                    ["blocker"] = new PlannerQuestClientState("blocker", 2, 0, true, true)
                },
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));

            PlannerRaidDecisionSignals signals = PlannerRaidDecisionSignalBuilder.Build(
                Plan(new[] { "q1" }, new[] { Objective("q1", "c", PlannerRaidObjectiveKind.Extract, "woods") }),
                topology,
                state);

            Assert.Equal(1, signals.ImmediateUnlockCount);
        }

        [Fact]
        public void Build_ExposesPreparationFrictionInsteadOfHidingItInRanking()
        {
            PlannerRaidPreparation preparation = new PlannerRaidPreparation(
                new[]
                {
                    new PlannerRaidBringNeed("key", 1, 0, 1, new[] { "q1" }),
                    new PlannerRaidBringNeed("marker", 1, 1, 0, new[] { "q1" })
                },
                new[]
                {
                    new PlannerRaidUnresolvedBringNeed("q1", "c2", "PlantItem", new[] { "a", "b" }, 1)
                });
            PlannerRaidPlan plan = new PlannerRaidPlan(
                "customs",
                new[] { "q1" },
                new[] { Objective("q1", "c1", PlannerRaidObjectiveKind.Plant, "customs", "marker") },
                preparation);

            PlannerRaidDecisionSignals signals = PlannerRaidDecisionSignalBuilder.Build(plan, Topology(Quest("q1")), EmptyState());

            Assert.False(signals.PreparationReady);
            Assert.Equal(1, signals.MissingPreparationTemplateCount);
            Assert.Equal(1, signals.UnresolvedPreparationCount);
        }

        private static PlannerRaidPlan Plan(IReadOnlyList<string> questIds, IReadOnlyList<PlannerRaidObjective> objectives)
        {
            return new PlannerRaidPlan(
                "customs",
                questIds,
                objectives,
                new PlannerRaidPreparation(
                    Array.Empty<PlannerRaidBringNeed>(),
                    Array.Empty<PlannerRaidUnresolvedBringNeed>()));
        }

        private static PlannerRaidObjective Objective(
            string questId,
            string conditionId,
            PlannerRaidObjectiveKind kind,
            string location,
            params string[] targets)
        {
            return new PlannerRaidObjective(
                questId,
                conditionId,
                kind,
                kind.ToString(),
                location,
                targets,
                false,
                1,
                0);
        }

        private static PlannerTopologyQuest Quest(string id, bool repeatable = false)
        {
            return new PlannerTopologyQuest(
                id,
                null,
                null,
                null,
                repeatable,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static PlannerTopologyIndex Topology(params PlannerTopologyQuest[] quests)
        {
            Dictionary<string, PlannerTopologyQuest> values = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
            foreach (PlannerTopologyQuest quest in quests) values[quest.QuestId] = quest;
            return new PlannerTopologyIndex(
                values,
                new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));
        }

        private static PlannerClientIndex EmptyState()
        {
            return new PlannerClientIndex(
                0,
                new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal),
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
        }
    }
}
