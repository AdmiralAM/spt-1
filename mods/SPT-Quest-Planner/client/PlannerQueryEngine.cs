using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerQueryEngine
    {
        private const int CompletedDisposition = 5;
        private const int ActiveDisposition = 4;
        private const int AvailableDisposition = 3;
        private const int SuccessProfileState = 4;
        private const int RawSuccessStatus = 4;
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

            HashSet<string> blockers = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < quest.PrerequisiteEdges.Count; i++)
            {
                PlannerTopologyPrerequisite prerequisite = quest.PrerequisiteEdges[i];
                if (!IsPrerequisiteSatisfied(prerequisite)) blockers.Add(prerequisite.SourceQuestId);
            }

            string[] result = new string[blockers.Count];
            blockers.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        public IReadOnlyList<PlannerTopologyPrerequisite> GetTerminalPrerequisiteConflicts(string questId)
        {
            PlannerTopologyQuest quest = topology.GetQuest(questId);
            if (quest == null) return Array.Empty<PlannerTopologyPrerequisite>();

            List<PlannerTopologyPrerequisite> conflicts = new List<PlannerTopologyPrerequisite>();
            for (int i = 0; i < quest.PrerequisiteEdges.Count; i++)
            {
                PlannerTopologyPrerequisite prerequisite = quest.PrerequisiteEdges[i];
                if (IsPrerequisiteSatisfied(prerequisite)) continue;
                if (AcceptsHypotheticalSuccess(prerequisite)) continue;

                PlannerTopologyQuest sourceQuest = topology.GetQuest(prerequisite.SourceQuestId);
                if (sourceQuest == null || sourceQuest.Repeatable) continue;
                PlannerQuestClientState sourceState = state.GetQuest(prerequisite.SourceQuestId);
                if (sourceState == null || sourceState.Disposition != CompletedDisposition) continue;

                conflicts.Add(prerequisite);
            }

            conflicts.Sort((a, b) =>
            {
                int source = string.Compare(a.SourceQuestId, b.SourceQuestId, StringComparison.Ordinal);
                if (source != 0) return source;
                return string.Compare(a.TargetQuestId, b.TargetQuestId, StringComparison.Ordinal);
            });
            return conflicts.ToArray();
        }

        public IReadOnlyList<string> GetImmediateUnlocksIfCompleted(string questId)
        {
            PlannerTopologyQuest completedQuest = topology.GetQuest(questId);
            if (completedQuest == null) return Array.Empty<string>();

            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < completedQuest.DependentEdges.Count; i++)
            {
                PlannerTopologyPrerequisite completedEdge = completedQuest.DependentEdges[i];
                if (!string.Equals(completedEdge.SourceQuestId, questId, StringComparison.Ordinal)) continue;
                if (!AcceptsHypotheticalSuccess(completedEdge)) continue;
                if (completedEdge.AvailableAfterSeconds > 0) continue;

                PlannerTopologyQuest dependent = topology.GetQuest(completedEdge.TargetQuestId);
                if (dependent == null || !dependent.StartConditionCoverageComplete) continue;

                PlannerQuestClientState dependentState = state.GetQuest(dependent.QuestId);
                if (dependentState != null)
                {
                    if (!dependentState.LevelGateSatisfied) continue;
                    if (dependentState.Disposition == ActiveDisposition || dependentState.Disposition == AvailableDisposition || dependentState.Disposition == CompletedDisposition) continue;
                }

                bool unlocked = true;
                for (int p = 0; p < dependent.PrerequisiteEdges.Count; p++)
                {
                    PlannerTopologyPrerequisite prerequisite = dependent.PrerequisiteEdges[p];
                    if (string.Equals(prerequisite.SourceQuestId, questId, StringComparison.Ordinal))
                    {
                        if (!AcceptsHypotheticalSuccess(prerequisite) || prerequisite.AvailableAfterSeconds > 0)
                        {
                            unlocked = false;
                            break;
                        }
                        continue;
                    }

                    if (!IsPrerequisiteSatisfied(prerequisite))
                    {
                        unlocked = false;
                        break;
                    }
                }

                if (unlocked) result.Add(dependent.QuestId);
            }

            string[] ordered = new string[result.Count];
            result.CopyTo(ordered);
            Array.Sort(ordered, StringComparer.Ordinal);
            return ordered;
        }

        public IReadOnlyList<string> GetIncompletePrerequisitePlan(string targetQuestId)
        {
            PlannerTopologyQuest target = topology.GetQuest(targetQuestId);
            if (target == null) return Array.Empty<string>();

            HashSet<string> closure = CollectUnsatisfiedAncestorClosure(targetQuestId);
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
                for (int i = 0; i < quest.PrerequisiteEdges.Count; i++)
                {
                    PlannerTopologyPrerequisite prerequisite = quest.PrerequisiteEdges[i];
                    if (!closure.Contains(prerequisite.SourceQuestId) || IsPrerequisiteSatisfied(prerequisite)) continue;
                    indegree[questId] = indegree[questId] + 1;
                    outgoing[prerequisite.SourceQuestId].Add(questId);
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
            HashSet<string> closure = CollectUnsatisfiedAncestorClosure(questId);
            string[] result = new string[closure.Count];
            closure.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private HashSet<string> CollectUnsatisfiedAncestorClosure(string questId)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            Stack<string> pending = new Stack<string>();
            PlannerTopologyQuest target = topology.GetQuest(questId);
            if (target == null) return visited;

            PushUnsatisfiedPrerequisites(target, pending);
            while (pending.Count > 0)
            {
                string currentId = pending.Pop();
                if (!visited.Add(currentId)) continue;
                if (visited.Count > maxVisitedNodes)
                    throw new InvalidOperationException("Quest Planner query exceeded the configured node traversal limit.");

                PlannerTopologyQuest current = topology.GetQuest(currentId);
                if (current == null) continue;
                PushUnsatisfiedPrerequisites(current, pending);
            }

            return visited;
        }

        private void PushUnsatisfiedPrerequisites(PlannerTopologyQuest quest, Stack<string> pending)
        {
            for (int i = 0; i < quest.PrerequisiteEdges.Count; i++)
            {
                PlannerTopologyPrerequisite prerequisite = quest.PrerequisiteEdges[i];
                if (!IsPrerequisiteSatisfied(prerequisite)) pending.Push(prerequisite.SourceQuestId);
            }
        }

        private bool IsPrerequisiteSatisfied(PlannerTopologyPrerequisite prerequisite)
        {
            PlannerQuestClientState source = state.GetQuest(prerequisite.SourceQuestId);
            if (prerequisite.HasRawProfileStatusContract && source != null && source.RawProfileStatus >= 0)
                return prerequisite.AcceptsRawProfileStatus(source.RawProfileStatus);

            int sourceProfileState = EffectiveProfileState(source);
            return prerequisite.AcceptsProfileState(sourceProfileState);
        }

        private static bool AcceptsHypotheticalSuccess(PlannerTopologyPrerequisite prerequisite)
        {
            if (prerequisite.HasRawProfileStatusContract)
                return prerequisite.AcceptsRawProfileStatus(RawSuccessStatus);
            return prerequisite.AcceptsProfileState(SuccessProfileState);
        }

        private static int EffectiveProfileState(PlannerQuestClientState questState)
        {
            if (questState == null) return 0;
            if (questState.ProfileState >= 0 && questState.ProfileState <= 5 && questState.ProfileState != 0) return questState.ProfileState;
            if (questState.Disposition == CompletedDisposition) return 4;
            if (questState.Disposition == ActiveDisposition) return 3;
            if (questState.Disposition == AvailableDisposition) return 2;
            return questState.ProfileState;
        }

        private bool IsCompleted(string questId)
        {
            PlannerQuestClientState questState = state.GetQuest(questId);
            return questState != null && questState.Disposition == CompletedDisposition;
        }
    }
}
