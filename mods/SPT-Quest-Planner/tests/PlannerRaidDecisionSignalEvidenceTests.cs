using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidDecisionSignalEvidenceTests
    {
        [Fact]
        public void BuilderPreservesExactQuestAndUnlockIdsForExplanation()
        {
            PlannerTopologyQuest main = new PlannerTopologyQuest(
                "main", null, null, null, false,
                Array.Empty<string>(), new[] { "followup" }, Array.Empty<string>());
            PlannerTopologyQuest daily = new PlannerTopologyQuest(
                "daily", null, null, null, true,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
            PlannerTopologyQuest followup = new PlannerTopologyQuest(
                "followup", null, null, null, false,
                new[] { "main" }, Array.Empty<string>(), Array.Empty<string>());

            PlannerTopologyIndex topology = new PlannerTopologyIndex(
                new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
                {
                    ["main"] = main,
                    ["daily"] = daily,
                    ["followup"] = followup
                },
                new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal));

            PlannerRaidPlan plan = new PlannerRaidPlan(
                "customs",
                new[] { "daily", "main" },
                new[]
                {
                    new PlannerRaidObjective("main", "m", PlannerRaidObjectiveKind.Extract, "Extract", "customs", Array.Empty<string>(), false, 1, 0),
                    new PlannerRaidObjective("daily", "d", PlannerRaidObjectiveKind.Kill, "Kill", "customs", new[] { "scav" }, false, 1, 0)
                },
                new PlannerRaidPreparation(Array.Empty<PlannerRaidBringNeed>(), Array.Empty<PlannerRaidUnresolvedBringNeed>()));

            PlannerClientIndex state = new PlannerClientIndex(
                0,
                new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal),
                new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));

            PlannerRaidDecisionSignals signals = PlannerRaidDecisionSignalBuilder.Build(plan, topology, state);

            Assert.Equal(new[] { "main" }, signals.NonRepeatableQuestIds);
            Assert.Equal(new[] { "daily" }, signals.RepeatableQuestIds);
            Assert.Equal(new[] { "followup" }, signals.ImmediateUnlockQuestIds);
        }
    }
}
