using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidOpportunity
    {
        public PlannerRaidOpportunity(
            string locationId,
            IReadOnlyList<string> questIds,
            IReadOnlyList<PlannerLocationObjective> objectives,
            int locationSpecificObjectiveCount,
            int globalObjectiveCount)
        {
            LocationId = locationId ?? string.Empty;
            QuestIds = questIds ?? Array.Empty<string>();
            Objectives = objectives ?? Array.Empty<PlannerLocationObjective>();
            LocationSpecificObjectiveCount = locationSpecificObjectiveCount;
            GlobalObjectiveCount = globalObjectiveCount;
        }

        public string LocationId { get; private set; }
        public IReadOnlyList<string> QuestIds { get; private set; }
        public IReadOnlyList<PlannerLocationObjective> Objectives { get; private set; }
        public int QuestCount { get { return QuestIds.Count; } }
        public int ObjectiveCount { get { return Objectives.Count; } }
        public int LocationSpecificObjectiveCount { get; private set; }
        public int GlobalObjectiveCount { get; private set; }
    }

    public static class PlannerRaidOpportunityBuilder
    {
        private const int DispositionAvailable = 3;
        private const int DispositionActive = 4;

        public static IReadOnlyList<PlannerRaidOpportunity> Build(
            PlannerLocationIndex locations,
            PlannerClientIndex state,
            bool includeAvailable = false,
            int maxLocations = 64,
            int maxObjectivesPerLocation = 512)
        {
            if (locations == null) throw new ArgumentNullException("locations");
            if (state == null) throw new ArgumentNullException("state");
            if (maxLocations <= 0) throw new ArgumentOutOfRangeException("maxLocations");
            if (maxObjectivesPerLocation <= 0) throw new ArgumentOutOfRangeException("maxObjectivesPerLocation");

            PlannerLocationObjective[] activeGlobal = locations.GlobalObjectives
                .Where(value => IsRaidActionable(value) && IsRelevantQuest(state, value.QuestId, includeAvailable))
                .ToArray();

            List<PlannerRaidOpportunity> result = new List<PlannerRaidOpportunity>();
            foreach (KeyValuePair<string, PlannerLocationBucket> pair in locations.Locations
                .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (result.Count >= maxLocations) break;

                List<PlannerLocationObjective> specific = pair.Value.Objectives
                    .Where(value => IsRaidActionable(value) && IsRelevantQuest(state, value.QuestId, includeAvailable))
                    .Take(maxObjectivesPerLocation)
                    .ToList();
                if (specific.Count == 0) continue;

                HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
                List<PlannerLocationObjective> combined = new List<PlannerLocationObjective>();
                AddUnique(combined, identities, specific, maxObjectivesPerLocation);
                int specificCount = combined.Count;
                AddUnique(combined, identities, activeGlobal, maxObjectivesPerLocation);
                int globalCount = combined.Count - specificCount;

                string[] questIds = combined
                    .Select(value => value.QuestId)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();

                result.Add(new PlannerRaidOpportunity(
                    pair.Key,
                    questIds,
                    combined.ToArray(),
                    specificCount,
                    globalCount));
            }

            return result
                .OrderByDescending(value => value.QuestCount)
                .ThenByDescending(value => value.LocationSpecificObjectiveCount)
                .ThenByDescending(value => value.ObjectiveCount)
                .ThenBy(value => value.LocationId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool IsRelevantQuest(PlannerClientIndex state, string questId, bool includeAvailable)
        {
            PlannerQuestClientState quest = state.GetQuest(questId);
            if (quest == null) return false;
            if (quest.Disposition == DispositionActive) return true;
            return includeAvailable && quest.Disposition == DispositionAvailable;
        }

        private static bool IsRaidActionable(PlannerLocationObjective objective)
        {
            if (objective == null || !string.Equals(objective.Phase, "Finish", StringComparison.OrdinalIgnoreCase)) return false;
            if (objective.Kind == PlannerObjectiveKind.LocationConstraint || objective.Kind == PlannerObjectiveKind.HandoverItem) return false;
            if (string.Equals(objective.ConditionType, "CounterCreator", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static void AddUnique(
            List<PlannerLocationObjective> output,
            HashSet<string> identities,
            IEnumerable<PlannerLocationObjective> source,
            int limit)
        {
            foreach (PlannerLocationObjective value in source)
            {
                if (output.Count >= limit) return;
                string key = value.QuestId + "\u001f" + value.ConditionId + "\u001f" + value.ConditionType;
                if (identities.Add(key)) output.Add(value);
            }
        }
    }
}
