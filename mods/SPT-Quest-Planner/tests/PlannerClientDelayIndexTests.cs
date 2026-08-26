using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerClientDelayIndexTests
    {
        [Fact]
        public void Build_PreservesKnownPendingDelayFromCachedStateJson()
        {
            string json = "{\"generatedAtUnixSeconds\":1000,\"player\":{\"questStates\":{\"q1\":{\"questId\":\"q1\",\"rawStatus\":9,\"availableAfterUnixSeconds\":1120}}}}";

            PlannerClientDelayIndex index = PlannerClientDelayIndexBuilder.Build(json);
            PlannerClientQuestDelay delay = index.GetQuest("q1");

            Assert.NotNull(delay);
            Assert.Equal(PlannerClientDelayState.PendingKnown, delay.State);
            Assert.Equal(120, delay.RemainingSeconds);
            Assert.True(delay.BlocksRaidAction);
        }

        [Fact]
        public void Build_DoesNotTreatStaleAvailableAfterAsDelayWhenRawStatusMovedOn()
        {
            string json = "{\"generatedAtUnixSeconds\":1000,\"player\":{\"questStates\":{\"q1\":{\"questId\":\"q1\",\"rawStatus\":1,\"availableAfterUnixSeconds\":1120}}}}";

            PlannerClientQuestDelay delay = PlannerClientDelayIndexBuilder.Build(json).GetQuest("q1");

            Assert.Equal(PlannerClientDelayState.NotDelayed, delay.State);
            Assert.False(delay.BlocksRaidAction);
        }

        [Fact]
        public void Build_ElapsedTimestampWaitsForAuthoritativeProfileRefresh()
        {
            string json = "{\"generatedAtUnixSeconds\":1200,\"player\":{\"questStates\":{\"q1\":{\"questId\":\"q1\",\"rawStatus\":9,\"availableAfterUnixSeconds\":1120}}}}";

            PlannerClientQuestDelay delay = PlannerClientDelayIndexBuilder.Build(json).GetQuest("q1");

            Assert.Equal(PlannerClientDelayState.ElapsedPendingRefresh, delay.State);
            Assert.Equal(0, delay.RemainingSeconds);
            Assert.True(delay.BlocksRaidAction);
        }

        [Fact]
        public void FocusEvidence_SeparatesWaitingFrontierBranchesFromRaidWork()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "goal",
                new[] { "wait-known", "wait-unknown", "raid-now" },
                new[] { "wait-known", "wait-unknown", "raid-now" },
                new[] { "raid-now" });
            PlannerClientDelayIndex delays = new PlannerClientDelayIndex(
                1000,
                new Dictionary<string, PlannerClientQuestDelay>(StringComparer.Ordinal)
                {
                    ["wait-known"] = new PlannerClientQuestDelay("wait-known", PlannerClientDelayState.PendingKnown, 1100, 100),
                    ["wait-unknown"] = new PlannerClientQuestDelay("wait-unknown", PlannerClientDelayState.TimingUnresolved, null, null),
                    ["raid-now"] = new PlannerClientQuestDelay("raid-now", PlannerClientDelayState.NotDelayed, null, null)
                });

            PlannerRaidFocusDelayEvidence evidence = PlannerRaidFocusDelayEvidenceBuilder.Build(intent, delays);

            Assert.Equal(new[] { "wait-known" }, evidence.PendingKnownQuestIds);
            Assert.Equal(new[] { "wait-unknown" }, evidence.TimingUnresolvedQuestIds);
            Assert.Empty(evidence.ElapsedPendingRefreshQuestIds);
            Assert.True(evidence.HasWaitingBranches);
        }
    }
}
