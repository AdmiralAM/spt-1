using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRaidOpportunity
    {
        public PlannerRaidOpportunity(string locationId, IReadOnlyList<string> questIds, IReadOnlyList<PlannerLocationObjective> objectives, IReadOnlyList<PlannerRaidObjective> raidObjectives, int locationSpecificObjectiveCount, int globalObjectiveCount)
        {
            LocationId = locationId ?? string.Empty;
            QuestIds = questIds ?? Array.Empty<string>();
            Objectives = objectives ?? Array.Empty<PlannerLocationObjective>();
            RaidObjectives = raidObjectives ?? Array.Empty<PlannerRaidObjective>();
            LocationSpecificObjectiveCount = locationSpecificObjectiveCount;
            GlobalObjectiveCount = globalObjectiveCount;
        }
        public string LocationId { get; private set; }
        public IReadOnlyList<string> QuestIds { get; private set; }
        public IReadOnlyList<PlannerLocationObjective> Objectives { get; private set; }
        public IReadOnlyList<PlannerRaidObjective> RaidObjectives { get; private set; }
        public int QuestCount { get { return QuestIds.Count; } }
        public int ObjectiveCount { get { return Objectives.Count; } }
        public int LocationSpecificObjectiveCount { get; private set; }
        public int GlobalObjectiveCount { get; private set; }
        public int KillCount { get { return Count(PlannerRaidObjectiveKind.Kill); } }
        public int VisitCount { get { return Count(PlannerRaidObjectiveKind.Visit); } }
        public int PlantCount { get { return Count(PlannerRaidObjectiveKind.Plant); } }
        public int FindCount { get { return Count(PlannerRaidObjectiveKind.Find); } }
        public int ExtractCount { get { return Count(PlannerRaidObjectiveKind.Extract); } }
        public int OtherCount { get { return Count(PlannerRaidObjectiveKind.Other); } }
        private int Count(PlannerRaidObjectiveKind kind) { int count = 0; for (int i = 0; i < RaidObjectives.Count; i++) if (RaidObjectives[i].Kind == kind) count++; return count; }
    }

    public static class PlannerRaidOpportunityBuilder
    {
        public const string AnyLocationId = "__any_location__";
        private const int DispositionAvailable = 3;
        private const int DispositionActive = 4;

        public static IReadOnlyList<PlannerRaidOpportunity> Build(PlannerLocationIndex locations, PlannerClientIndex state, bool includeAvailable = false, int maxLocations = 64, int maxObjectivesPerLocation = 128)
        {
            if (locations == null) throw new ArgumentNullException("locations");
            if (state == null) throw new ArgumentNullException("state");
            if (maxLocations <= 0) throw new ArgumentOutOfRangeException("maxLocations");
            if (maxObjectivesPerLocation <= 0) throw new ArgumentOutOfRangeException("maxObjectivesPerLocation");

            List<PlannerRaidOpportunity> result = new List<PlannerRaidOpportunity>();

            foreach (KeyValuePair<string, PlannerLocationBucket> pair in locations.Locations.OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (result.Count >= maxLocations) break;
                if (!IsPlausibleLocationId(pair.Key)) continue;

                List<PlannerLocationObjective> specific = pair.Value.Objectives
                    .Where(value => IsRaidActionable(value) && IsRelevantQuest(state, value.QuestId, includeAvailable) && !IsCompleted(state, value))
                    .Take(maxObjectivesPerLocation)
                    .ToList();
                if (specific.Count == 0) continue;

                HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
                List<PlannerLocationObjective> unique = new List<PlannerLocationObjective>();
                AddUnique(unique, identities, specific, maxObjectivesPerLocation);
                result.Add(CreateOpportunity(pair.Key, unique, unique.Count, 0, state));
            }

            // Global/any-map objectives are useful, but copying them into every map made each plan
            // look like the entire quest database. Keep them as one explicit opportunity instead.
            if (result.Count < maxLocations)
            {
                PlannerLocationObjective[] global = locations.GlobalObjectives
                    .Where(value => IsRaidActionable(value) && IsRelevantQuest(state, value.QuestId, includeAvailable) && !IsCompleted(state, value))
                    .Take(maxObjectivesPerLocation)
                    .ToArray();
                if (global.Length > 0)
                {
                    HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
                    List<PlannerLocationObjective> unique = new List<PlannerLocationObjective>();
                    AddUnique(unique, identities, global, maxObjectivesPerLocation);
                    result.Add(CreateOpportunity(AnyLocationId, unique, 0, unique.Count, state));
                }
            }

            return result
                .OrderBy(value => value.LocationId == AnyLocationId ? 1 : 0)
                .ThenByDescending(value => value.QuestCount)
                .ThenByDescending(value => value.LocationSpecificObjectiveCount)
                .ThenByDescending(value => value.ObjectiveCount)
                .ThenBy(value => value.LocationId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static PlannerRaidOpportunity CreateOpportunity(string locationId, IReadOnlyList<PlannerLocationObjective> objectives, int specificCount, int globalCount, PlannerClientIndex state)
        {
            string[] questIds = objectives.Select(value => value.QuestId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            PlannerRaidObjective[] raidObjectives = objectives
                .Select(value => PlannerRaidObjectiveNormalizer.Normalize(value, locationId, GetProgress(state, value)))
                .OrderBy(value => value.Kind)
                .ThenBy(value => value.QuestId, StringComparer.Ordinal)
                .ThenBy(value => value.ConditionId, StringComparer.Ordinal)
                .ToArray();
            return new PlannerRaidOpportunity(locationId, questIds, objectives, raidObjectives, specificCount, globalCount);
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
            if (objective.Kind == PlannerObjectiveKind.Other || objective.Kind == PlannerObjectiveKind.LocationConstraint || objective.Kind == PlannerObjectiveKind.HandoverItem) return false;
            if (string.Equals(objective.ConditionType, "CounterCreator", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static bool IsPlausibleLocationId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string trimmed = value.Trim();
            if (trimmed.IndexOf("SPTarkov.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.IndexOf("System.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.IndexOf("ListOrT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.IndexOf('`') >= 0 || trimmed.IndexOf('[') >= 0 || trimmed.IndexOf(']') >= 0)
                return false;
            return trimmed.Length <= 128;
        }

        private static PlannerConditionProgress GetProgress(PlannerClientIndex state, PlannerLocationObjective objective)
        {
            if (objective == null) return null;
            PlannerConditionProgress progress = state.GetConditionProgress(objective.ConditionId);
            if (progress == null && !string.IsNullOrWhiteSpace(objective.ParentConditionId)) progress = state.GetConditionProgress(objective.ParentConditionId);
            if (progress != null && !string.IsNullOrWhiteSpace(progress.SourceQuestId) && !string.Equals(progress.SourceQuestId, objective.QuestId, StringComparison.Ordinal)) return null;
            return progress;
        }

        private static bool IsCompleted(PlannerClientIndex state, PlannerLocationObjective objective)
        {
            if (objective == null || !objective.RequiredValue.HasValue || objective.RequiredValue.Value <= 0d) return false;
            PlannerConditionProgress progress = GetProgress(state, objective);
            return progress != null && progress.Value >= objective.RequiredValue.Value;
        }

        private static void AddUnique(List<PlannerLocationObjective> output, HashSet<string> identities, IEnumerable<PlannerLocationObjective> source, int limit)
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
