using System;
using System.Collections.Generic;

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
            List<string> result = new List<string>();

            foreach (KeyValuePair<string, PlannerQuestClientState> entry in state.Quests)
            {
                PlannerQuestClientState quest = entry.Value;
                if (quest == null || string.IsNullOrWhiteSpace(quest.QuestId)) continue;
                if (!Included(quest.Disposition, policy)) continue;

                if (result.Count >= MaxCandidates)
                    throw new InvalidOperationException("Quest candidate selection exceeds bounded candidate limit of " + MaxCandidates + ". Narrow the policy before ranking.");

                result.Add(quest.QuestId);
            }

            result.Sort(StringComparer.Ordinal);
            return result;
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
