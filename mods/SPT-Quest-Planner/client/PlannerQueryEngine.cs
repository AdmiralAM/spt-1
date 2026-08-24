using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerQueryEngine
    {
        private const int CompletedDisposition = 5;
        private readonly PlannerTopologyIndex topology;
        private readonly PlannerClientIndex state;
        private readonly int maxVisitedNodes;

        public PlannerQueryEngine(PlannerTopologyIndex topology, PlannerClientIndex state, int maxVisitedNodes = 10000)
        {
            this.topology = topology ?? throw new ArgumentNullException("topology");
            this.state = state ?? throw new ArgumentNullException("state");
            if (maxVisitedNodes <= 0) throw new ArgumentOutOfRangeException("maxVisitedNodes");
            this.maxVisitedNodes = maxVisitedNodes;
        }

        public IReadOnlyList<string> GetImmediateBlockers(string questId)
        {
            PlannerTopologyQuest quest = topology.GetQuest(questId);
            if (quest == null) return Array.Empty<string>();

            List<string> result = new List<string>();
            for (int i = 0; i < quest.PrerequisiteQuestIds.Count; i++)
            {
                string prerequisiteId = quest.PrerequisiteQuestIds[i];
                if (!IsCompleted(prerequisiteId)) result.Add(prerequisiteId);
            }
            return result;
        }

        public IReadOnlyList<string> GetImmediateUnlocksIfCompleted(string questId)
        {
            PlannerTopologyQuest completedQuest = topology.GetQuest(questId);
            if (completedQuest == null) return Array.Empty<string>();

            List<string> result = new List<string>();
            for (int i = 0; i < completedQuest.DependentQuestIds.Count; i++)
            {
                string dependentId = completedQuest.DependentQuestIds[i];
                PlannerTopologyQuest dependent = topology.GetQuest(dependentId);
                if (dependent == null) continue;

                bool unlocked = true;
                for (int p = 0; p < dependent.PrerequisiteQuestIds.Count; p++)
                {
                    string prerequisiteId = dependent.PrerequisiteQuestIds[p];
                    if (string.Equals(prerequisiteId, questId, StringComparison.Ordinal)) continue;
                    if (IsCompleted(prerequisiteId)) continue;
                    unlocked = false;
                    break;
                }

                if (unlocked) result.Add(dependentId);
            }
            return result;
        }

        public IReadOnlyList<string> GetIncompletePrerequisitePlan(string targetQuestId)
        {
            PlannerTopologyQuest target = topology.GetQuest(targetQuestId);
            if (target == null) return Array.Empty<string>();

            HashSet<string> closure = CollectAncestorClosure(targetQuestId);
            if (!IsCompleted(targetQuestId)) closure.Add(targetQuestId);
            if (closure.Count == 0) return Array.Empty<string>();

            Dictionary<string, int> indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, List<string>> outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string questId in closure)
            {
                indegree[questId] = 0;
                outgoing[questId] = new List<string>();
            }

            foreach (string questId in closure)
            {
                PlannerTopologyQuest quest = topology.GetQuest(questId);
                if (quest == null) continue;
                for (int i = 0; i < quest.PrerequisiteQuestIds.Count; i++)
                {
                    string prerequisiteId = quest.PrerequisiteQuestIds[i];
                    if (!closure.Contains(prerequisiteId)) continue;
                    indegree[questId] = indegree[questId] + 1;
                    outgoing[prerequisiteId].Add(questId);
                }
            }

            SortedSet<string> ready = new SortedSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in indegree)
                if (pair.Value == 0) ready.Add(pair.Key);

            List<string> ordered = new List<string>(closure.Count);
            while (ready.Count > 0)
            {
                string questId = ready.Min;
                ready.Remove(questId);
                ordered.Add(questId);

                List<string> dependents = outgoing[questId];
                dependents.Sort(StringComparer.Ordinal);
                for (int i = 0; i < dependents.Count; i++)
                {
                    string dependentId = dependents[i];
                    int next = indegree[dependentId] - 1;
                    indegree[dependentId] = next;
                    if (next == 0) ready.Add(dependentId);
                }
            }

            if (ordered.Count != closure.Count)
                throw new InvalidOperationException("Quest prerequisite plan contains a cycle or incomplete topology.");

            return ordered;
        }

        public IReadOnlyList<string> GetIncompleteAncestors(string questId)
        {
            HashSet<string> closure = CollectAncestorClosure(questId);
            string[] result = new string[closure.Count];
            closure.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private HashSet<string> CollectAncestorClosure(string questId)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Stack<string> pending = new Stack<string>();
            PlannerTopologyQuest target = topology.GetQuest(questId);
            if (target == null) return visited;

            for (int i = 0; i < target.PrerequisiteQuestIds.Count; i++)
                pending.Push(target.PrerequisiteQuestIds[i]);

            while (pending.Count > 0)
            {
                string currentId = pending.Pop();
                if (visited.Contains(currentId)) continue;
                if (IsCompleted(currentId)) continue;

                visited.Add(currentId);
                if (visited.Count > maxVisitedNodes)
                    throw new InvalidOperationException("Quest Planner query exceeded the configured node traversal limit.");

                PlannerTopologyQuest current = topology.GetQuest(currentId);
                if (current == null) continue;
                for (int i = 0; i < current.PrerequisiteQuestIds.Count; i++)
                    pending.Push(current.PrerequisiteQuestIds[i]);
            }

            return visited;
        }

        private bool IsCompleted(string questId)
        {
            PlannerQuestClientState questState = state.GetQuest(questId);
            return questState != null && questState.Disposition == CompletedDisposition;
        }
    }
}
