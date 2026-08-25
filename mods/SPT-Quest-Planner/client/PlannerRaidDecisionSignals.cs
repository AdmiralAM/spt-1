using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidActionOverlap
    {
        public PlannerRaidActionOverlap(
            string signature,
            PlannerRaidObjectiveKind kind,
            IReadOnlyList<string> questIds,
            int objectiveCount)
        {
            Signature = signature ?? string.Empty;
            Kind = kind;
            QuestIds = questIds ?? Array.Empty<string>();
            ObjectiveCount = Math.Max(0, objectiveCount);
        }

        public string Signature { get; private set; }
        public PlannerRaidObjectiveKind Kind { get; private set; }
        public IReadOnlyList<string> QuestIds { get; private set; }
        public int ObjectiveCount { get; private set; }
        public int QuestCount { get { return QuestIds.Count; } }
    }

    public sealed class PlannerRaidDecisionSignals
    {
        public PlannerRaidDecisionSignals(
            int nonRepeatableQuestCount,
            int repeatableQuestCount,
            IReadOnlyList<PlannerRaidActionOverlap> actionOverlaps,
            int immediateUnlockCount,
            int missingPreparationTemplateCount,
            int unresolvedPreparationCount,
            int unknownObjectiveCount,
            int objectiveCount,
            double knownRemainingWork)
        {
            NonRepeatableQuestCount = Math.Max(0, nonRepeatableQuestCount);
            RepeatableQuestCount = Math.Max(0, repeatableQuestCount);
            ActionOverlaps = actionOverlaps ?? Array.Empty<PlannerRaidActionOverlap>();
            ImmediateUnlockCount = Math.Max(0, immediateUnlockCount);
            MissingPreparationTemplateCount = Math.Max(0, missingPreparationTemplateCount);
            UnresolvedPreparationCount = Math.Max(0, unresolvedPreparationCount);
            UnknownObjectiveCount = Math.Max(0, unknownObjectiveCount);
            ObjectiveCount = Math.Max(0, objectiveCount);
            KnownRemainingWork = Math.Max(0d, knownRemainingWork);
        }

        public int NonRepeatableQuestCount { get; private set; }
        public int RepeatableQuestCount { get; private set; }
        public IReadOnlyList<PlannerRaidActionOverlap> ActionOverlaps { get; private set; }
        public int ImmediateUnlockCount { get; private set; }
        public int MissingPreparationTemplateCount { get; private set; }
        public int UnresolvedPreparationCount { get; private set; }
        public int UnknownObjectiveCount { get; private set; }
        public int ObjectiveCount { get; private set; }
        public double KnownRemainingWork { get; private set; }

        public int CrossQuestOverlapGroupCount { get { return ActionOverlaps.Count; } }
        public int MaxOverlappingQuestCount
        {
            get { return ActionOverlaps.Count == 0 ? 0 : ActionOverlaps.Max(value => value.QuestCount); }
        }
        public bool PreparationReady
        {
            get { return MissingPreparationTemplateCount == 0 && UnresolvedPreparationCount == 0; }
        }
        public double RepeatableShare
        {
            get
            {
                int total = NonRepeatableQuestCount + RepeatableQuestCount;
                return total == 0 ? 0d : (double)RepeatableQuestCount / total;
            }
        }
        public double EvidenceCoverage
        {
            get { return ObjectiveCount == 0 ? 1d : Math.Max(0d, 1d - ((double)UnknownObjectiveCount / ObjectiveCount)); }
        }
    }

    public static class PlannerRaidDecisionSignalBuilder
    {
        public static PlannerRaidDecisionSignals Build(
            PlannerRaidPlan plan,
            PlannerTopologyIndex topology,
            PlannerClientIndex state)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            if (topology == null) throw new ArgumentNullException("topology");
            if (state == null) throw new ArgumentNullException("state");

            int nonRepeatableQuestCount = 0;
            int repeatableQuestCount = 0;
            HashSet<string> seenQuestIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.QuestIds.Count; i++)
            {
                string questId = plan.QuestIds[i];
                if (string.IsNullOrWhiteSpace(questId) || !seenQuestIds.Add(questId)) continue;
                PlannerTopologyQuest quest = topology.GetQuest(questId);
                if (quest != null && quest.Repeatable) repeatableQuestCount++;
                else nonRepeatableQuestCount++;
            }

            Dictionary<string, MutableOverlap> overlapBySignature = new Dictionary<string, MutableOverlap>(StringComparer.Ordinal);
            int unknownObjectiveCount = 0;
            for (int i = 0; i < plan.Objectives.Count; i++)
            {
                PlannerRaidObjective objective = plan.Objectives[i];
                if (objective == null) continue;
                if (objective.Kind == PlannerRaidObjectiveKind.Other)
                {
                    unknownObjectiveCount++;
                    continue;
                }

                string signature = BuildActionSignature(objective);
                MutableOverlap overlap;
                if (!overlapBySignature.TryGetValue(signature, out overlap))
                {
                    overlap = new MutableOverlap(signature, objective.Kind);
                    overlapBySignature[signature] = overlap;
                }
                overlap.ObjectiveCount++;
                if (!string.IsNullOrWhiteSpace(objective.QuestId)) overlap.QuestIds.Add(objective.QuestId);
            }

            List<PlannerRaidActionOverlap> actionOverlaps = new List<PlannerRaidActionOverlap>();
            foreach (MutableOverlap overlap in overlapBySignature.Values)
            {
                if (overlap.QuestIds.Count < 2) continue;
                string[] questIds = overlap.QuestIds.ToArray();
                Array.Sort(questIds, StringComparer.Ordinal);
                actionOverlaps.Add(new PlannerRaidActionOverlap(
                    overlap.Signature,
                    overlap.Kind,
                    questIds,
                    overlap.ObjectiveCount));
            }
            actionOverlaps.Sort((a, b) =>
            {
                int questCount = b.QuestCount.CompareTo(a.QuestCount);
                if (questCount != 0) return questCount;
                int objectiveCount = b.ObjectiveCount.CompareTo(a.ObjectiveCount);
                if (objectiveCount != 0) return objectiveCount;
                return string.Compare(a.Signature, b.Signature, StringComparison.Ordinal);
            });

            PlannerQueryEngine query = new PlannerQueryEngine(topology, state);
            HashSet<string> immediateUnlocks = new HashSet<string>(StringComparer.Ordinal);
            foreach (string questId in seenQuestIds)
            {
                PlannerTopologyQuest quest = topology.GetQuest(questId);
                if (quest == null || quest.Repeatable) continue;
                IReadOnlyList<string> unlocks = query.GetImmediateUnlocksIfCompleted(questId);
                for (int i = 0; i < unlocks.Count; i++)
                    if (!string.IsNullOrWhiteSpace(unlocks[i])) immediateUnlocks.Add(unlocks[i]);
            }

            return new PlannerRaidDecisionSignals(
                nonRepeatableQuestCount,
                repeatableQuestCount,
                actionOverlaps.ToArray(),
                immediateUnlocks.Count,
                plan.MissingBringTemplateCount,
                plan.UnresolvedPreparationCount,
                unknownObjectiveCount,
                plan.ObjectiveCount,
                plan.KnownRemainingWork);
        }

        private static string BuildActionSignature(PlannerRaidObjective objective)
        {
            string[] targets = objective.Targets == null
                ? Array.Empty<string>()
                : objective.Targets.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            Array.Sort(targets, StringComparer.Ordinal);

            return ((int)objective.Kind).ToString() + "|" +
                   (objective.LocationId ?? string.Empty).Trim().ToLowerInvariant() + "|" +
                   string.Join(",", targets);
        }

        private sealed class MutableOverlap
        {
            public MutableOverlap(string signature, PlannerRaidObjectiveKind kind)
            {
                Signature = signature;
                Kind = kind;
            }

            public string Signature;
            public PlannerRaidObjectiveKind Kind;
            public HashSet<string> QuestIds = new HashSet<string>(StringComparer.Ordinal);
            public int ObjectiveCount;
        }
    }
}
