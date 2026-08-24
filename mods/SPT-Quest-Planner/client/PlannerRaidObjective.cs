using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public enum PlannerRaidObjectiveKind { Other = 0, Kill = 1, Visit = 2, Plant = 3, Find = 4, Extract = 5, Bring = 6 }

    public sealed class PlannerRaidObjective
    {
        public PlannerRaidObjective(string questId, string conditionId, PlannerRaidObjectiveKind kind, string conditionType, string locationId, IReadOnlyList<string> targets, bool global, double? requiredValue = null, double? currentValue = null)
        {
            QuestId = questId ?? string.Empty;
            ConditionId = conditionId ?? string.Empty;
            Kind = kind;
            ConditionType = conditionType ?? string.Empty;
            LocationId = locationId ?? string.Empty;
            Targets = targets ?? Array.Empty<string>();
            Global = global;
            RequiredValue = requiredValue;
            CurrentValue = currentValue;
            RemainingValue = requiredValue.HasValue ? Math.Max(0d, requiredValue.Value - (currentValue ?? 0d)) : (double?)null;
        }
        public string QuestId { get; private set; }
        public string ConditionId { get; private set; }
        public PlannerRaidObjectiveKind Kind { get; private set; }
        public string ConditionType { get; private set; }
        public string LocationId { get; private set; }
        public IReadOnlyList<string> Targets { get; private set; }
        public bool Global { get; private set; }
        public double? RequiredValue { get; private set; }
        public double? CurrentValue { get; private set; }
        public double? RemainingValue { get; private set; }
        public bool HasProgress { get { return RequiredValue.HasValue && CurrentValue.HasValue; } }
    }

    public static class PlannerRaidObjectiveNormalizer
    {
        public static PlannerRaidObjective Normalize(PlannerLocationObjective objective, string effectiveLocationId, PlannerConditionProgress progress = null)
        {
            if (objective == null) throw new ArgumentNullException("objective");
            return new PlannerRaidObjective(objective.QuestId, objective.ConditionId, Classify(objective.ConditionType), objective.ConditionType, effectiveLocationId, objective.Targets == null ? Array.Empty<string>() : objective.Targets.ToArray(), objective.LocationIds == null || objective.LocationIds.Count == 0, objective.RequiredValue, progress == null ? (double?)null : progress.Value);
        }

        public static PlannerRaidObjectiveKind Classify(string conditionType)
        {
            string type = (conditionType ?? string.Empty).Trim();
            if (EqualsAny(type, "Kills", "Kill", "KillCondition", "Elimination")) return PlannerRaidObjectiveKind.Kill;
            if (EqualsAny(type, "VisitPlace", "VisitLocation", "Zone", "EnterZone")) return PlannerRaidObjectiveKind.Visit;
            if (EqualsAny(type, "PlaceBeacon", "LeaveItemAtLocation", "PlaceItem", "PlantItem", "MarkObject")) return PlannerRaidObjectiveKind.Plant;
            if (EqualsAny(type, "FindItem", "FindQuestItem", "PickupItem", "ObtainItem")) return PlannerRaidObjectiveKind.Find;
            if (EqualsAny(type, "ExitStatus", "Extract", "Extraction", "Survive")) return PlannerRaidObjectiveKind.Extract;
            if (EqualsAny(type, "HandoverItem", "BringItem")) return PlannerRaidObjectiveKind.Bring;
            return PlannerRaidObjectiveKind.Other;
        }

        private static bool EqualsAny(string value, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++) if (string.Equals(value, candidates[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
