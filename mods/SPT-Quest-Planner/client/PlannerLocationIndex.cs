using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SPTQuestPlanner.Client
{
    public enum PlannerObjectiveKind
    {
        Other = 0,
        Kill = 1,
        Visit = 2,
        Extract = 3,
        FindItem = 4,
        HandoverItem = 5,
        Plant = 6,
        LocationConstraint = 7
    }

    public sealed class PlannerLocationObjective
    {
        public PlannerLocationObjective(
            string questId,
            string conditionId,
            string conditionType,
            string phase,
            string parentConditionId,
            IReadOnlyList<string> targets,
            IReadOnlyList<string> locationIds,
            PlannerObjectiveKind kind)
        {
            QuestId = questId ?? string.Empty;
            ConditionId = conditionId ?? string.Empty;
            ConditionType = conditionType ?? string.Empty;
            Phase = phase ?? string.Empty;
            ParentConditionId = parentConditionId;
            Targets = targets ?? Array.Empty<string>();
            LocationIds = locationIds ?? Array.Empty<string>();
            Kind = kind;
        }

        public string QuestId { get; private set; }
        public string ConditionId { get; private set; }
        public string ConditionType { get; private set; }
        public string Phase { get; private set; }
        public string ParentConditionId { get; private set; }
        public IReadOnlyList<string> Targets { get; private set; }
        public IReadOnlyList<string> LocationIds { get; private set; }
        public PlannerObjectiveKind Kind { get; private set; }
    }

    public sealed class PlannerLocationBucket
    {
        public PlannerLocationBucket(string locationId, IReadOnlyList<PlannerLocationObjective> objectives)
        {
            LocationId = locationId ?? string.Empty;
            Objectives = objectives ?? Array.Empty<PlannerLocationObjective>();
            QuestIds = Objectives.Select(value => value.QuestId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public string LocationId { get; private set; }
        public IReadOnlyList<PlannerLocationObjective> Objectives { get; private set; }
        public IReadOnlyList<string> QuestIds { get; private set; }
    }

    public sealed class PlannerLocationIndex
    {
        public PlannerLocationIndex(
            IReadOnlyDictionary<string, PlannerLocationBucket> locations,
            IReadOnlyList<PlannerLocationObjective> globalObjectives)
        {
            Locations = locations ?? new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase);
            GlobalObjectives = globalObjectives ?? Array.Empty<PlannerLocationObjective>();
        }

        public IReadOnlyDictionary<string, PlannerLocationBucket> Locations { get; private set; }
        public IReadOnlyList<PlannerLocationObjective> GlobalObjectives { get; private set; }

        public PlannerLocationBucket GetLocation(string locationId)
        {
            PlannerLocationBucket value;
            return !string.IsNullOrWhiteSpace(locationId) && Locations.TryGetValue(locationId, out value) ? value : null;
        }
    }

    public static class PlannerLocationIndexBuilder
    {
        public static PlannerLocationIndex Build(string topologyJson)
        {
            if (string.IsNullOrWhiteSpace(topologyJson)) throw new ArgumentException("Topology JSON is missing.", "topologyJson");
            object root = Parse(topologyJson);
            List<MutableObjective> raw = new List<MutableObjective>();

            foreach (object objective in Values(Get(root, "questObjectives")))
            {
                string questId = ReadString(Get(objective, "questId"));
                if (string.IsNullOrWhiteSpace(questId)) continue;
                raw.Add(new MutableObjective(
                    questId,
                    ReadString(Get(objective, "conditionId")) ?? string.Empty,
                    ReadString(Get(objective, "conditionType")) ?? string.Empty,
                    ReadString(Get(objective, "phase")) ?? string.Empty,
                    ReadString(Get(objective, "parentConditionId")),
                    ReadStrings(Get(objective, "targets")),
                    ReadStrings(Get(objective, "locationHints")),
                    NormalizeLocation(ReadString(Get(objective, "questLocationHint")))));
            }

            Dictionary<string, HashSet<string>> groupLocations = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            for (int i = 0; i < raw.Count; i++)
            {
                MutableObjective value = raw[i];
                if (string.IsNullOrWhiteSpace(value.ParentConditionId) || value.LocationHints.Count == 0) continue;
                string key = GroupKey(value.QuestId, value.ParentConditionId);
                HashSet<string> locations;
                if (!groupLocations.TryGetValue(key, out locations))
                {
                    locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    groupLocations[key] = locations;
                }
                foreach (string location in value.LocationHints) locations.Add(location);
            }

            Dictionary<string, List<PlannerLocationObjective>> byLocation = new Dictionary<string, List<PlannerLocationObjective>>(StringComparer.OrdinalIgnoreCase);
            List<PlannerLocationObjective> global = new List<PlannerLocationObjective>();

            for (int i = 0; i < raw.Count; i++)
            {
                MutableObjective value = raw[i];
                HashSet<string> effective = new HashSet<string>(value.LocationHints, StringComparer.OrdinalIgnoreCase);
                if (effective.Count == 0 && !string.IsNullOrWhiteSpace(value.ParentConditionId))
                {
                    HashSet<string> inherited;
                    if (groupLocations.TryGetValue(GroupKey(value.QuestId, value.ParentConditionId), out inherited))
                        effective.UnionWith(inherited);
                }
                if (effective.Count == 0 && !string.IsNullOrWhiteSpace(value.QuestLocationHint))
                    effective.Add(value.QuestLocationHint);

                string[] locations = effective.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();
                PlannerLocationObjective frozen = new PlannerLocationObjective(
                    value.QuestId,
                    value.ConditionId,
                    value.ConditionType,
                    value.Phase,
                    value.ParentConditionId,
                    value.Targets,
                    locations,
                    Classify(value.ConditionType));

                if (locations.Length == 0)
                {
                    global.Add(frozen);
                    continue;
                }

                for (int locationIndex = 0; locationIndex < locations.Length; locationIndex++)
                {
                    List<PlannerLocationObjective> bucket;
                    if (!byLocation.TryGetValue(locations[locationIndex], out bucket))
                    {
                        bucket = new List<PlannerLocationObjective>();
                        byLocation[locations[locationIndex]] = bucket;
                    }
                    bucket.Add(frozen);
                }
            }

            Dictionary<string, PlannerLocationBucket> frozenLocations = new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<PlannerLocationObjective>> pair in byLocation)
            {
                PlannerLocationObjective[] objectives = pair.Value
                    .OrderBy(value => value.QuestId, StringComparer.Ordinal)
                    .ThenBy(value => value.ConditionId, StringComparer.Ordinal)
                    .ToArray();
                frozenLocations[pair.Key] = new PlannerLocationBucket(pair.Key, objectives);
            }

            return new PlannerLocationIndex(
                frozenLocations,
                global.OrderBy(value => value.QuestId, StringComparer.Ordinal)
                    .ThenBy(value => value.ConditionId, StringComparer.Ordinal)
                    .ToArray());
        }

        private static PlannerObjectiveKind Classify(string conditionType)
        {
            string value = conditionType ?? string.Empty;
            if (value.Equals("Location", StringComparison.OrdinalIgnoreCase)) return PlannerObjectiveKind.LocationConstraint;
            if (value.IndexOf("kill", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("elimination", StringComparison.OrdinalIgnoreCase) >= 0)
                return PlannerObjectiveKind.Kill;
            if (value.Equals("VisitPlace", StringComparison.OrdinalIgnoreCase) || value.IndexOf("visit", StringComparison.OrdinalIgnoreCase) >= 0)
                return PlannerObjectiveKind.Visit;
            if (value.Equals("ExitStatus", StringComparison.OrdinalIgnoreCase) || value.IndexOf("extract", StringComparison.OrdinalIgnoreCase) >= 0)
                return PlannerObjectiveKind.Extract;
            if (value.Equals("FindItem", StringComparison.OrdinalIgnoreCase)) return PlannerObjectiveKind.FindItem;
            if (value.Equals("HandoverItem", StringComparison.OrdinalIgnoreCase)) return PlannerObjectiveKind.HandoverItem;
            if (value.Equals("PlaceBeacon", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("LeaveItemAtLocation", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("PlaceItem", StringComparison.OrdinalIgnoreCase) ||
                value.IndexOf("beacon", StringComparison.OrdinalIgnoreCase) >= 0)
                return PlannerObjectiveKind.Plant;
            return PlannerObjectiveKind.Other;
        }

        private static string GroupKey(string questId, string parentConditionId) { return questId + "\u001f" + parentConditionId; }

        private static string NormalizeLocation(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string normalized = value.Trim();
            if (string.Equals(normalized, "any", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "anywhere", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "none", StringComparison.OrdinalIgnoreCase))
                return null;
            return normalized;
        }

        private static string[] ReadStrings(object token)
        {
            List<string> result = new List<string>();
            foreach (object value in Values(token))
            {
                string text = ReadString(value);
                if (!string.IsNullOrWhiteSpace(text)) result.Add(text);
            }
            return result.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static object Parse(string json)
        {
            Type tokenType = FindType("Newtonsoft.Json.Linq.JToken");
            if (tokenType == null) throw new InvalidOperationException("Newtonsoft JSON runtime is unavailable.");
            MethodInfo parse = tokenType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (parse == null) throw new InvalidOperationException("Newtonsoft JToken.Parse is unavailable.");
            return parse.Invoke(null, new object[] { json });
        }

        private static object Get(object token, string name)
        {
            if (token == null) return null;
            PropertyInfo stringItem = token.GetType().GetProperty("Item", new[] { typeof(string) });
            if (stringItem != null)
            {
                try { return stringItem.GetValue(token, new object[] { name }); } catch { }
            }
            PropertyInfo objectItem = token.GetType().GetProperty("Item", new[] { typeof(object) });
            if (objectItem != null)
            {
                try { return objectItem.GetValue(token, new object[] { name }); } catch { }
            }
            return null;
        }

        private static IEnumerable<object> Values(object token)
        {
            if (token == null) yield break;
            IEnumerable sequence = token as IEnumerable;
            if (sequence == null || token is string) yield break;
            foreach (object value in sequence) yield return value;
        }

        private static string ReadString(object token) { return token == null ? null : token.ToString(); }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type type = assemblies[i].GetType(fullName, false, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        private sealed class MutableObjective
        {
            public MutableObjective(
                string questId,
                string conditionId,
                string conditionType,
                string phase,
                string parentConditionId,
                IReadOnlyList<string> targets,
                IReadOnlyList<string> locationHints,
                string questLocationHint)
            {
                QuestId = questId;
                ConditionId = conditionId;
                ConditionType = conditionType;
                Phase = phase;
                ParentConditionId = parentConditionId;
                Targets = targets;
                LocationHints = locationHints;
                QuestLocationHint = questLocationHint;
            }

            public string QuestId;
            public string ConditionId;
            public string ConditionType;
            public string Phase;
            public string ParentConditionId;
            public IReadOnlyList<string> Targets;
            public IReadOnlyList<string> LocationHints;
            public string QuestLocationHint;
        }
    }
}
