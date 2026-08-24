using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerActivePlanSnapshot
    {
        public PlannerActivePlanSnapshot(
            long cacheRevision,
            string locationId,
            string locationName,
            int questCount,
            int objectiveCount,
            bool preparationReady,
            int missingBringTemplateCount,
            int unresolvedPreparationCount,
            IReadOnlyList<PlannerRaidObjective> objectives,
            IReadOnlyList<PlannerRaidBringNeed> bringNeeds)
        {
            CacheRevision = cacheRevision;
            LocationId = locationId ?? string.Empty;
            LocationName = string.IsNullOrWhiteSpace(locationName) ? LocationId : locationName;
            QuestCount = Math.Max(0, questCount);
            ObjectiveCount = Math.Max(0, objectiveCount);
            PreparationReady = preparationReady;
            MissingBringTemplateCount = Math.Max(0, missingBringTemplateCount);
            UnresolvedPreparationCount = Math.Max(0, unresolvedPreparationCount);
            Objectives = objectives ?? Array.Empty<PlannerRaidObjective>();
            BringNeeds = bringNeeds ?? Array.Empty<PlannerRaidBringNeed>();
        }

        public long CacheRevision { get; private set; }
        public string LocationId { get; private set; }
        public string LocationName { get; private set; }
        public int QuestCount { get; private set; }
        public int ObjectiveCount { get; private set; }
        public bool PreparationReady { get; private set; }
        public int MissingBringTemplateCount { get; private set; }
        public int UnresolvedPreparationCount { get; private set; }
        public IReadOnlyList<PlannerRaidObjective> Objectives { get; private set; }
        public IReadOnlyList<PlannerRaidBringNeed> BringNeeds { get; private set; }
        public bool HasPlan { get { return !string.IsNullOrWhiteSpace(LocationId); } }
        public bool NeedsAttention { get { return MissingBringTemplateCount > 0 || UnresolvedPreparationCount > 0; } }

        public static PlannerActivePlanSnapshot Empty(long cacheRevision)
        {
            return new PlannerActivePlanSnapshot(
                cacheRevision,
                string.Empty,
                string.Empty,
                0,
                0,
                false,
                0,
                0,
                Array.Empty<PlannerRaidObjective>(),
                Array.Empty<PlannerRaidBringNeed>());
        }
    }
}
