using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerCandidatePolicy
    {
        public PlannerCandidatePolicy(bool includeActive = true, bool includeAvailable = true, bool includeReachable = true, bool includeBlocked = false)
        {
            IncludeActive = includeActive;
            IncludeAvailable = includeAvailable;
            IncludeReachable = includeReachable;
            IncludeBlocked = includeBlocked;
        }

        public bool IncludeActive { get; private set; }
        public bool IncludeAvailable { get; private set; }
        public bool IncludeReachable { get; private set; }
        public bool IncludeBlocked { get; private set; }
    }

    public sealed class PlannerCandidateSelector
    {
        private const int MaxCandidates = 256;
        private const int BlockedDisposition = 1;
        private const int ReachableDisposition = 2;
        private const int AvailableDisposition = 3;
        private const int ActiveDisposition = 4;

        private readonly PlannerClientIndex state;

        public PlannerCandidateSelector(PlannerClientIndex state)
        {
            this.state = state ?? throw new ArgumentNullException("state");
        }

        public IReadOnlyList<string> Select(PlannerCandidatePolicy policy = null)
        {
            policy = policy ?? new PlannerCandidatePolicy();

            // The installed quest set can contain thousands of reachable quests. That is normal and
            // must not make recommendations fail. Select a bounded working set deterministically,
            // prioritising what the player can act on now before speculative future progression.
            return state.Quests.Values
                .Where(quest => quest != null && !string.IsNullOrWhiteSpace(quest.QuestId) && Included(quest.Disposition, policy))
                .OrderBy(quest => Priority(quest.Disposition))
                .ThenBy(quest => quest.QuestId, StringComparer.Ordinal)
                .Take(MaxCandidates)
                .Select(quest => quest.QuestId)
                .ToArray();
        }

        private static int Priority(int disposition)
        {
            switch (disposition)
            {
                case ActiveDisposition: return 0;
                case AvailableDisposition: return 1;
                case ReachableDisposition: return 2;
                case BlockedDisposition: return 3;
                default: return 4;
            }
        }

        private static bool Included(int disposition, PlannerCandidatePolicy policy)
        {
            switch (disposition)
            {
                case ActiveDisposition: return policy.IncludeActive;
                case AvailableDisposition: return policy.IncludeAvailable;
                case ReachableDisposition: return policy.IncludeReachable;
                case BlockedDisposition: return policy.IncludeBlocked;
                default: return false;
            }
        }
    }
}
