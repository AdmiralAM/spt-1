using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public enum PlannerRaidObjectiveKind
    {
        Other = 0,
        Kill = 1,
        Visit = 2,
        Plant = 3,
        Find = 4,
        Extract = 5,
        Bring = 6
    }

    public sealed class PlannerRaidObjective
    {
        public PlannerRaidObjective(
            string questId,
            string conditionId,
            PlannerRaidObjectiveKind kind,
            string conditionType,
            string locationId,
            IReadOnlyList<string> targets,
            bool global)
        {
            QuestId = questId ?? string.Empty;
            ConditionId = conditionId ?? string.Empty;
            Kind = kind;
            ConditionType = conditionType ?? string.Empty;
            LocationId = locationId ?? string.Empty;
            Targets = targets ?? Array.Empty<string>();
            Global = global;
        }

        public string QuestId { get; private set; }
        public string ConditionId { get; private set; }
        public PlannerRaidObjectiveKind Kind { get; private set; }
        public string ConditionType { get; private set; }
        public string LocationId { get; private set; }
        public IReadOnlyList<string> Targets { get; private set; }
        public bool Global { get; private set; }
    }

    public static class PlannerRaidObjectiveNormalizer
    {
        public static PlannerRaidObjective Normalize(PlannerLocationObjective objective, string effectiveLocationId)
        {
            if (objective == null) throw new ArgumentNullException("objective");
            bool global = objective.LocationIds == null || objective.LocationIds.Count == 0;
            return new PlannerRaidObjective(
                objective.QuestId,
                objective.ConditionId,
                Classify(objective.ConditionType),
                objective.ConditionType,
                effectiveLocationId,
                objective.Targets == null ? Array.Empty<string>() : objective.Targets.ToArray(),
                global);
        }

        public static PlannerRaidObjectiveKind Classify(string conditionType)
        {
            string type = (conditionType ?? string.Empty).Trim();
            if (type.Length == 0) return PlannerRaidObjectiveKind.Other;

            if (EqualsAny(type, "Kills", "Kill", "KillCondition", "Elimination"))
                return PlannerRaidObjectiveKind.Kill;

            if (EqualsAny(type, "VisitPlace", "VisitLocation", "Zone", "EnterZone"))
                return PlannerRaidObjectiveKind.Visit;

            if (EqualsAny(type, "PlaceBeacon", "LeaveItemAtLocation", "PlaceItem", "PlantItem", "MarkObject"))
                return PlannerRaidObjectiveKind.Plant;

            if (EqualsAny(type, "FindItem", "FindQuestItem", "PickupItem", "ObtainItem"))
                return PlannerRaidObjectiveKind.Find;

            if (EqualsAny(type, "ExitStatus", "Extract", "Extraction", "Survive"))
                return PlannerRaidObjectiveKind.Extract;

            if (EqualsAny(type, "HandoverItem", "BringItem"))
                return PlannerRaidObjectiveKind.Bring;

            return PlannerRaidObjectiveKind.Other;
        }

        private static bool EqualsAny(string value, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
                if (string.Equals(value, candidates[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
