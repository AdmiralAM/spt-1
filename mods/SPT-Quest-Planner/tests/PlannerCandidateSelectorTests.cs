using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerCandidateSelectorTests
    {
        [Fact]
        public void DefaultPolicy_SelectsActiveAvailableAndReachableOnly()
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
        public void Selection_IsDeterministic()
        {
            PlannerCandidateSelector selector = new PlannerCandidateSelector(State(
                Quest("z", 4), Quest("a", 3), Quest("m", 2)));

            Assert.Equal(new[] { "a", "m", "z" }, selector.Select());
        }

        [Fact]
        public void Selection_RejectsUnboundedCandidateSet()
        {
            Dictionary<string, PlannerQuestClientState> quests = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            for (int i = 0; i < 257; i++) quests["q" + i] = Quest("q" + i, 4);
            PlannerCandidateSelector selector = new PlannerCandidateSelector(new PlannerClientIndex(1, quests, null));

            Assert.Throws<InvalidOperationException>(() => selector.Select());
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
