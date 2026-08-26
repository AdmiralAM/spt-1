using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public enum PlannerCapabilityGoalCatalogState
    {
        NoActionProven = 0,
        Actionable = 1,
        Waiting = 2,
        EvidenceIncomplete = 3,
        ProgressionConflict = 4,
        AlreadyUnlocked = 5
    }

    public sealed class PlannerCapabilityGoalCatalogItem
    {
        public PlannerCapabilityGoalCatalogItem(
            PlannerCapabilityGoalDefinition definition,
            PlannerCapabilityGoalCatalogState state,
            int actionableQuestCount,
            int waitingQuestCount,
            int unknownQuestCount)
        {
            Definition = definition ?? throw new ArgumentNullException("definition");
            State = state;
            ActionableQuestCount = Math.Max(0, actionableQuestCount);
            WaitingQuestCount = Math.Max(0, waitingQuestCount);
            UnknownQuestCount = Math.Max(0, unknownQuestCount);
        }

        public PlannerCapabilityGoalDefinition Definition { get; private set; }
        public PlannerCapabilityGoalCatalogState State { get; private set; }
        public int ActionableQuestCount { get; private set; }
        public int WaitingQuestCount { get; private set; }
        public int UnknownQuestCount { get; private set; }
        public bool IsOpenGoal { get { return State != PlannerCapabilityGoalCatalogState.AlreadyUnlocked; } }
    }

    public sealed class PlannerCapabilityGoalCatalog
    {
        public PlannerCapabilityGoalCatalog(
            IReadOnlyList<PlannerCapabilityGoalCatalogItem> openGoals,
            IReadOnlyList<PlannerCapabilityGoalCatalogItem> unlockedGoals)
        {
            OpenGoals = openGoals ?? Array.Empty<PlannerCapabilityGoalCatalogItem>();
            UnlockedGoals = unlockedGoals ?? Array.Empty<PlannerCapabilityGoalCatalogItem>();
        }

        public IReadOnlyList<PlannerCapabilityGoalCatalogItem> OpenGoals { get; private set; }
        public IReadOnlyList<PlannerCapabilityGoalCatalogItem> UnlockedGoals { get; private set; }
    }

    public static class PlannerCapabilityGoalCatalogBuilder
    {
        public static PlannerCapabilityGoalCatalog Build(
            IReadOnlyList<PlannerCapabilityGoalDefinition> definitions,
            PlannerTopologyIndex topology,
            PlannerClientIndex state,
            PlannerClientDelayIndex delays,
            int maxGoals = 32)
        {
            if (definitions == null) throw new ArgumentNullException("definitions");
            if (topology == null) throw new ArgumentNullException("topology");
            if (state == null) throw new ArgumentNullException("state");
            if (delays == null) throw new ArgumentNullException("delays");
            if (maxGoals < 1 || maxGoals > 128) throw new ArgumentOutOfRangeException("maxGoals");
            if (definitions.Count > maxGoals)
                throw new InvalidOperationException("Capability goal catalog exceeds the bounded goal limit of " + maxGoals + ".");

            HashSet<string> capabilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<PlannerCapabilityGoalCatalogItem> open = new List<PlannerCapabilityGoalCatalogItem>();
            List<PlannerCapabilityGoalCatalogItem> unlocked = new List<PlannerCapabilityGoalCatalogItem>();

            for (int i = 0; i < definitions.Count; i++)
            {
                PlannerCapabilityGoalDefinition definition = definitions[i];
                if (definition == null) throw new InvalidOperationException("Capability goal catalog contains a null definition.");
                if (!capabilityIds.Add(definition.CapabilityId))
                    throw new InvalidOperationException("Capability goal catalog contains duplicate capability ID: " + definition.CapabilityId);

                PlannerCapabilityGoal goal = PlannerCapabilityGoalBuilder.Build(definition, topology, state);
                PlannerRaidFocusDelayEvidence delayEvidence = PlannerRaidFocusDelayEvidenceBuilder.Build(goal.QuestIntent, delays);
                PlannerCapabilityGoalCatalogState catalogState = Classify(goal, delayEvidence);
                int waitingCount = delayEvidence.PendingKnownQuestIds
                    .Concat(delayEvidence.ElapsedPendingRefreshQuestIds)
                    .Concat(delayEvidence.TimingUnresolvedQuestIds)
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                PlannerCapabilityGoalCatalogItem item = new PlannerCapabilityGoalCatalogItem(
                    definition,
                    catalogState,
                    goal.QuestIntent.FocusActionableQuestIds.Count,
                    waitingCount,
                    goal.QuestIntent.FocusEligibilityUnknownQuestIds.Count);

                if (item.IsOpenGoal) open.Add(item);
                else unlocked.Add(item);
            }

            return new PlannerCapabilityGoalCatalog(
                Order(open),
                Order(unlocked));
        }

        private static PlannerCapabilityGoalCatalogState Classify(
            PlannerCapabilityGoal goal,
            PlannerRaidFocusDelayEvidence delays)
        {
            if (goal.GateAlreadyCompleted)
                return PlannerCapabilityGoalCatalogState.AlreadyUnlocked;
            if (goal.HasTerminalConflict)
                return PlannerCapabilityGoalCatalogState.ProgressionConflict;
            if (goal.HasEligibilityUnknowns)
                return PlannerCapabilityGoalCatalogState.EvidenceIncomplete;
            if (goal.HasActionableQuestWork)
                return PlannerCapabilityGoalCatalogState.Actionable;
            if (delays.PendingKnownQuestIds.Count > 0 ||
                delays.ElapsedPendingRefreshQuestIds.Count > 0 ||
                delays.TimingUnresolvedQuestIds.Count > 0)
                return PlannerCapabilityGoalCatalogState.Waiting;
            return PlannerCapabilityGoalCatalogState.NoActionProven;
        }

        private static IReadOnlyList<PlannerCapabilityGoalCatalogItem> Order(
            IEnumerable<PlannerCapabilityGoalCatalogItem> values)
        {
            return values
                .OrderBy(value => value.Definition.CapabilityId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.Definition.GateQuestId, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
