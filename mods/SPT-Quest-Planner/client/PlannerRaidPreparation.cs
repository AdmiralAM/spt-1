using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidBringNeed
    {
        public PlannerRaidBringNeed(string templateId, double required, double owned, double missing, IReadOnlyList<string> questIds)
        {
            TemplateId = templateId ?? string.Empty;
            Required = Math.Max(0d, required);
            Owned = Math.Max(0d, owned);
            Missing = Math.Max(0d, missing);
            QuestIds = questIds ?? Array.Empty<string>();
        }

        public string TemplateId { get; private set; }
        public double Required { get; private set; }
        public double Owned { get; private set; }
        public double Missing { get; private set; }
        public IReadOnlyList<string> QuestIds { get; private set; }
        public bool Ready { get { return Missing <= 0.000001d; } }
    }

    public sealed class PlannerRaidUnresolvedBringNeed
    {
        public PlannerRaidUnresolvedBringNeed(string questId, string conditionId, string conditionType, IReadOnlyList<string> templateIds, double required)
        {
            QuestId = questId ?? string.Empty;
            ConditionId = conditionId ?? string.Empty;
            ConditionType = conditionType ?? string.Empty;
            TemplateIds = templateIds ?? Array.Empty<string>();
            Required = Math.Max(0d, required);
        }

        public string QuestId { get; private set; }
        public string ConditionId { get; private set; }
        public string ConditionType { get; private set; }
        public IReadOnlyList<string> TemplateIds { get; private set; }
        public double Required { get; private set; }
    }

    public sealed class PlannerRaidPreparation
    {
        public PlannerRaidPreparation(IReadOnlyList<PlannerRaidBringNeed> exactNeeds, IReadOnlyList<PlannerRaidUnresolvedBringNeed> unresolvedNeeds)
        {
            ExactNeeds = exactNeeds ?? Array.Empty<PlannerRaidBringNeed>();
            UnresolvedNeeds = unresolvedNeeds ?? Array.Empty<PlannerRaidUnresolvedBringNeed>();
        }

        public IReadOnlyList<PlannerRaidBringNeed> ExactNeeds { get; private set; }
        public IReadOnlyList<PlannerRaidUnresolvedBringNeed> UnresolvedNeeds { get; private set; }
        public int MissingTemplateCount { get { return ExactNeeds.Count(value => !value.Ready); } }
        public bool Ready { get { return MissingTemplateCount == 0 && UnresolvedNeeds.Count == 0; } }
    }

    public static class PlannerRaidPreparationBuilder
    {
        public static PlannerRaidPreparation Build(PlannerRaidPlan plan, PlannerClientIndex state)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            if (state == null) throw new ArgumentNullException("state");

            Dictionary<string, MutableNeed> exact = new Dictionary<string, MutableNeed>(StringComparer.Ordinal);
            List<PlannerRaidUnresolvedBringNeed> unresolved = new List<PlannerRaidUnresolvedBringNeed>();

            for (int i = 0; i < plan.Objectives.Count; i++)
            {
                PlannerRaidObjective objective = plan.Objectives[i];
                if (!IsProvenConsumablePlant(objective.ConditionType)) continue;

                double required = objective.RemainingValue ?? objective.RequiredValue ?? 1d;
                if (required <= 0d) continue;

                string[] targets = objective.Targets
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                if (targets.Length == 0) continue;

                if (targets.Length != 1)
                {
                    unresolved.Add(new PlannerRaidUnresolvedBringNeed(
                        objective.QuestId,
                        objective.ConditionId,
                        objective.ConditionType,
                        targets,
                        required));
                    continue;
                }

                MutableNeed need;
                if (!exact.TryGetValue(targets[0], out need))
                {
                    need = new MutableNeed(targets[0]);
                    exact[targets[0]] = need;
                }
                need.Required += required;
                need.QuestIds.Add(objective.QuestId);
            }

            PlannerRaidBringNeed[] frozen = exact.Values
                .Select(value =>
                {
                    PlannerOwnedItem owned = state.GetOwnedItem(value.TemplateId);
                    double ownedCount = owned == null ? 0d : owned.Total;
                    return new PlannerRaidBringNeed(
                        value.TemplateId,
                        value.Required,
                        ownedCount,
                        Math.Max(0d, value.Required - ownedCount),
                        value.QuestIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
                })
                .OrderByDescending(value => value.Missing)
                .ThenBy(value => value.TemplateId, StringComparer.Ordinal)
                .ToArray();

            return new PlannerRaidPreparation(
                frozen,
                unresolved.OrderBy(value => value.QuestId, StringComparer.Ordinal)
                    .ThenBy(value => value.ConditionId, StringComparer.Ordinal)
                    .ToArray());
        }

        private static bool IsProvenConsumablePlant(string conditionType)
        {
            return string.Equals(conditionType, "PlaceBeacon", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(conditionType, "LeaveItemAtLocation", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class MutableNeed
        {
            public MutableNeed(string templateId) { TemplateId = templateId; }
            public string TemplateId;
            public double Required;
            public HashSet<string> QuestIds = new HashSet<string>(StringComparer.Ordinal);
        }
    }
}
