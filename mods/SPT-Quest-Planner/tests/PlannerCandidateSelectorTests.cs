using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCandidateSelectorTests
    {
        [Fact]
        public void DefaultPolicy_SelectsActiveAvailableAndReachableOnly_InPriorityOrder()
        {
            PlannerCandidateSelector selector = new PlannerCandidateSelector(State(
                Quest("blocked", 1),
                Quest("reachable", 2),
                Quest("available", 3),
                Quest("active", 4),
                Quest("completed", 5),
                Quest("failed", 6)));

            IReadOnlyList<string> result = selector.Select();

            Assert.Equal(new[] { "active", "available", "reachable" }, result);
        }

        [Fact]
        public void Policy_CanIncludeBlockedAndExcludeReachable()
        {
            PlannerCandidateSelector selector = new PlannerCandidateSelector(State(
                Quest("blocked", 1), Quest("reachable", 2), Quest("active", 4)));

            IReadOnlyList<string> result = selector.Select(new PlannerCandidatePolicy(
                includeActive: true,
                includeAvailable: false,
                includeReachable: false,
                includeBlocked: true));

            Assert.Equal(new[] { "active", "blocked" }, result);
        }

        [Fact]
        public void Selection_IsDeterministicWithinPriorityBands()
        {
            PlannerCandidateSelector selector = new PlannerCandidateSelector(State(
                Quest("z-active", 4), Quest("a-available", 3), Quest("a-active", 4), Quest("m-reachable", 2)));

            Assert.Equal(new[] { "a-active", "z-active", "a-available", "m-reachable" }, selector.Select());
        }

        [Fact]
        public void Selection_CapsHugeReachableSetInsteadOfFailing()
        {
            Dictionary<string, PlannerQuestClientState> quests = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            quests["active"] = Quest("active", 4);
            quests["available"] = Quest("available", 3);
            for (int i = 0; i < 500; i++) quests["reachable-" + i.ToString("D3")] = Quest("reachable-" + i.ToString("D3"), 2);
            PlannerCandidateSelector selector = new PlannerCandidateSelector(new PlannerClientIndex(1, quests, null));

            IReadOnlyList<string> result = selector.Select();

            Assert.Equal(256, result.Count);
            Assert.Equal("active", result[0]);
            Assert.Equal("available", result[1]);
        }

        private static PlannerClientIndex State(params PlannerQuestClientState[] quests)
        {
            Dictionary<string, PlannerQuestClientState> map = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            for (int i = 0; i < quests.Length; i++) map[quests[i].QuestId] = quests[i];
            return new PlannerClientIndex(1, map, null);
        }

        private static PlannerQuestClientState Quest(string id, int disposition)
        {
            return new PlannerQuestClientState(id, disposition, 0, true, true);
        }
    }
}
