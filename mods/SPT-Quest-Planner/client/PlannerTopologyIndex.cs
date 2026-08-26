using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerTopologyPrerequisite
    {
        public PlannerTopologyPrerequisite(
            string sourceQuestId,
            string targetQuestId,
            IReadOnlyList<int> acceptedProfileStates,
            int availableAfterSeconds,
            IReadOnlyList<int> acceptedRawProfileStatuses = null)
        {
            SourceQuestId = sourceQuestId ?? string.Empty;
            TargetQuestId = targetQuestId ?? string.Empty;
            AcceptedProfileStates = acceptedProfileStates ?? Array.Empty<int>();
            AcceptedRawProfileStatuses = acceptedRawProfileStatuses ?? Array.Empty<int>();
            AvailableAfterSeconds = Math.Max(0, availableAfterSeconds);
        }

        public string SourceQuestId { get; private set; }
        public string TargetQuestId { get; private set; }
        public IReadOnlyList<int> AcceptedProfileStates { get; private set; }
        public IReadOnlyList<int> AcceptedRawProfileStatuses { get; private set; }
        public int AvailableAfterSeconds { get; private set; }
        public bool HasRawProfileStatusContract { get { return AcceptedRawProfileStatuses.Count > 0; } }

        public bool AcceptsProfileState(int profileState)
        {
            for (int i = 0; i < AcceptedProfileStates.Count; i++)
                if (AcceptedProfileStates[i] == profileState) return true;
            return false;
        }

        public bool AcceptsRawProfileStatus(int rawProfileStatus)
        {
            for (int i = 0; i < AcceptedRawProfileStatuses.Count; i++)
                if (AcceptedRawProfileStatuses[i] == rawProfileStatus) return true;
            return false;
        }
    }

    public sealed class PlannerTopologyQuest
    {
        public PlannerTopologyQuest(
            string questId,
            string traderId,
            string nameKey,
            int? minimumLevel,
            bool repeatable,
            IReadOnlyList<string> prerequisiteQuestIds,
            IReadOnlyList<string> dependentQuestIds,
            IReadOnlyList<string> requiredTemplateIds,
            bool startConditionCoverageComplete = true,
            IReadOnlyList<PlannerTopologyPrerequisite> prerequisiteEdges = null,
            IReadOnlyList<PlannerTopologyPrerequisite> dependentEdges = null)
        {
            QuestId = questId ?? string.Empty;
            TraderId = traderId;
            NameKey = nameKey;
            MinimumLevel = minimumLevel;
            Repeatable = repeatable;
            PrerequisiteQuestIds = prerequisiteQuestIds ?? Array.Empty<string>();
            DependentQuestIds = dependentQuestIds ?? Array.Empty<string>();
            RequiredTemplateIds = requiredTemplateIds ?? Array.Empty<string>();
            StartConditionCoverageComplete = startConditionCoverageComplete;
            PrerequisiteEdges = prerequisiteEdges ?? BuildDefaultPrerequisiteEdges(QuestId, PrerequisiteQuestIds);
            DependentEdges = dependentEdges ?? BuildDefaultDependentEdges(QuestId, DependentQuestIds);
        }

        public string QuestId { get; private set; }
        public string TraderId { get; private set; }
        public string NameKey { get; private set; }
        public int? MinimumLevel { get; private set; }
        public bool Repeatable { get; private set; }
        public IReadOnlyList<string> PrerequisiteQuestIds { get; private set; }
        public IReadOnlyList<string> DependentQuestIds { get; private set; }
        public IReadOnlyList<string> RequiredTemplateIds { get; private set; }
        public bool StartConditionCoverageComplete { get; private set; }
        public IReadOnlyList<PlannerTopologyPrerequisite> PrerequisiteEdges { get; private set; }
        public IReadOnlyList<PlannerTopologyPrerequisite> DependentEdges { get; private set; }

        private static IReadOnlyList<PlannerTopologyPrerequisite> BuildDefaultPrerequisiteEdges(
            string targetQuestId,
            IReadOnlyList<string> sourceQuestIds)
        {
            PlannerTopologyPrerequisite[] result = new PlannerTopologyPrerequisite[sourceQuestIds.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = new PlannerTopologyPrerequisite(sourceQuestIds[i], targetQuestId, new[] { 4 }, 0, new[] { 4 });
            return result;
        }

        private static IReadOnlyList<PlannerTopologyPrerequisite> BuildDefaultDependentEdges(
            string sourceQuestId,
            IReadOnlyList<string> targetQuestIds)
        {
            PlannerTopologyPrerequisite[] result = new PlannerTopologyPrerequisite[targetQuestIds.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = new PlannerTopologyPrerequisite(sourceQuestId, targetQuestIds[i], new[] { 4 }, 0, new[] { 4 });
            return result;
        }
    }

    public sealed class PlannerTopologyItem
    {
        public PlannerTopologyItem(string templateId, IReadOnlyList<string> questIds)
        {
            TemplateId = templateId ?? string.Empty;
            QuestIds = questIds ?? Array.Empty<string>();
        }

        public string TemplateId { get; private set; }
        public IReadOnlyList<string> QuestIds { get; private set; }
    }

    public sealed class PlannerTopologyIndex
    {
        public PlannerTopologyIndex(
            IReadOnlyDictionary<string, PlannerTopologyQuest> quests,
            IReadOnlyDictionary<string, PlannerTopologyItem> items)
        {
            Quests = quests ?? new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
            Items = items ?? new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, PlannerTopologyQuest> Quests { get; private set; }
        public IReadOnlyDictionary<string, PlannerTopologyItem> Items { get; private set; }

        public PlannerTopologyQuest GetQuest(string questId)
        {
            PlannerTopologyQuest value;
            return !string.IsNullOrWhiteSpace(questId) && Quests.TryGetValue(questId, out value) ? value : null;
        }

        public PlannerTopologyItem GetItem(string templateId)
        {
            PlannerTopologyItem value;
            return !string.IsNullOrWhiteSpace(templateId) && Items.TryGetValue(templateId, out value) ? value : null;
        }
    }

    public static class PlannerTopologyIndexBuilder
    {
        public static PlannerTopologyIndex Build(string topologyJson)
        {
            if (string.IsNullOrWhiteSpace(topologyJson)) throw new ArgumentException("Topology JSON is missing.", "topologyJson");
            object root = Parse(topologyJson);

            Dictionary<string, MutableQuest> quests = new Dictionary<string, MutableQuest>(StringComparer.Ordinal);
            foreach (object node in Values(Get(root, "questNodes")))
            {
                string questId = ReadString(Get(node, "questId"));
                if (string.IsNullOrWhiteSpace(questId)) continue;
                quests[questId] = new MutableQuest(
                    questId,
                    ReadString(Get(node, "traderId")),
                    ReadString(Get(node, "nameKey")),
                    ReadNullableInt(Get(node, "minimumLevel")),
                    ReadBool(Get(node, "repeatable"), false),
                    ReadBool(Get(node, "startConditionCoverageComplete"), true));
            }

            foreach (object edge in Values(Get(root, "prerequisites")))
            {
                string source = ReadString(Get(edge, "sourceQuestId"));
                string target = ReadString(Get(edge, "targetQuestId"));
                MutableQuest sourceQuest;
                MutableQuest targetQuest;
                if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(target) &&
                    quests.TryGetValue(source, out sourceQuest) && quests.TryGetValue(target, out targetQuest))
                {
                    int[] acceptedProfileStates = ReadProfileStates(Get(edge, "acceptedSourceStates"));
                    int[] acceptedRawProfileStatuses = ReadRawStatuses(Get(edge, "acceptedSourceRawStatuses"));
                    int availableAfterSeconds = Math.Max(0, ReadInt(Get(edge, "availableAfterSeconds"), 0));
                    PlannerTopologyPrerequisite prerequisite = new PlannerTopologyPrerequisite(
                        source,
                        target,
                        acceptedProfileStates,
                        availableAfterSeconds,
                        acceptedRawProfileStatuses);

                    targetQuest.Prerequisites.Add(source);
                    sourceQuest.Dependents.Add(target);
                    targetQuest.PrerequisiteEdges.Add(prerequisite);
                    sourceQuest.DependentEdges.Add(prerequisite);
                }
            }

            Dictionary<string, HashSet<string>> itemQuests = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (object requirement in Values(Get(root, "itemRequirements")))
            {
                string questId = ReadString(Get(requirement, "questId"));
                MutableQuest quest = null;
                bool knownQuest = !string.IsNullOrWhiteSpace(questId) && quests.TryGetValue(questId, out quest);
                foreach (object templateNode in Values(Get(requirement, "templateIds")))
                {
                    string templateId = ReadString(templateNode);
                    if (string.IsNullOrWhiteSpace(templateId)) continue;
                    if (knownQuest && quest != null) quest.RequiredTemplates.Add(templateId);
                    HashSet<string> linkedQuests;
                    if (!itemQuests.TryGetValue(templateId, out linkedQuests))
                    {
                        linkedQuests = new HashSet<string>(StringComparer.Ordinal);
                        itemQuests[templateId] = linkedQuests;
                    }
                    if (!string.IsNullOrWhiteSpace(questId)) linkedQuests.Add(questId);
                }
            }

            Dictionary<string, PlannerTopologyQuest> frozenQuests = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, MutableQuest> pair in quests)
            {
                MutableQuest value = pair.Value;
                frozenQuests[pair.Key] = new PlannerTopologyQuest(
                    value.QuestId,
                    value.TraderId,
                    value.NameKey,
                    value.MinimumLevel,
                    value.Repeatable,
                    Sorted(value.Prerequisites),
                    Sorted(value.Dependents),
                    Sorted(value.RequiredTemplates),
                    value.StartConditionCoverageComplete,
                    SortedEdges(value.PrerequisiteEdges),
                    SortedEdges(value.DependentEdges));
            }

            Dictionary<string, PlannerTopologyItem> frozenItems = new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, HashSet<string>> pair in itemQuests)
                frozenItems[pair.Key] = new PlannerTopologyItem(pair.Key, Sorted(pair.Value));

            return new PlannerTopologyIndex(frozenQuests, frozenItems);
        }

        private static string[] Sorted(HashSet<string> values)
        {
            string[] result = new string[values.Count];
            values.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static PlannerTopologyPrerequisite[] SortedEdges(List<PlannerTopologyPrerequisite> values)
        {
            PlannerTopologyPrerequisite[] result = values.ToArray();
            Array.Sort(result, (a, b) =>
            {
                int source = string.Compare(a.SourceQuestId, b.SourceQuestId, StringComparison.Ordinal);
                if (source != 0) return source;
                int target = string.Compare(a.TargetQuestId, b.TargetQuestId, StringComparison.Ordinal);
                if (target != 0) return target;
                return a.AvailableAfterSeconds.CompareTo(b.AvailableAfterSeconds);
            });
            return result;
        }

        private static int[] ReadProfileStates(object token)
        {
            List<int> values = new List<int>();
            foreach (object node in Values(token))
            {
                int parsed;
                if (TryReadProfileState(node, out parsed) && !values.Contains(parsed)) values.Add(parsed);
            }
            values.Sort();
            return values.ToArray();
        }

        private static int[] ReadRawStatuses(object token)
        {
            List<int> values = new List<int>();
            foreach (object node in Values(token))
            {
                int parsed;
                if (int.TryParse(ReadString(node), out parsed) && parsed >= 0 && parsed <= 9 && !values.Contains(parsed)) values.Add(parsed);
            }
            values.Sort();
            return values.ToArray();
        }

        private static bool TryReadProfileState(object token, out int value)
        {
            string text = ReadString(token);
            if (int.TryParse(text, out value)) return value >= 0 && value <= 5;
            if (string.Equals(text, "Unknown", StringComparison.OrdinalIgnoreCase)) { value = 0; return true; }
            if (string.Equals(text, "Locked", StringComparison.OrdinalIgnoreCase)) { value = 1; return true; }
            if (string.Equals(text, "Available", StringComparison.OrdinalIgnoreCase)) { value = 2; return true; }
            if (string.Equals(text, "Started", StringComparison.OrdinalIgnoreCase)) { value = 3; return true; }
            if (string.Equals(text, "Success", StringComparison.OrdinalIgnoreCase)) { value = 4; return true; }
            if (string.Equals(text, "Failed", StringComparison.OrdinalIgnoreCase)) { value = 5; return true; }
            value = 0;
            return false;
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
            if (sequence == null) yield break;
            foreach (object value in sequence) yield return value;
        }

        private static string ReadString(object token) { return token == null ? null : token.ToString(); }
        private static bool ReadBool(object token, bool fallback) { bool value; return bool.TryParse(ReadString(token), out value) ? value : fallback; }
        private static int ReadInt(object token, int fallback) { int value; return int.TryParse(ReadString(token), out value) ? value : fallback; }
        private static int? ReadNullableInt(object token) { int value; return int.TryParse(ReadString(token), out value) ? (int?)value : null; }

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

        private sealed class MutableQuest
        {
            public MutableQuest(
                string questId,
                string traderId,
                string nameKey,
                int? minimumLevel,
                bool repeatable,
                bool startConditionCoverageComplete)
            {
                QuestId = questId;
                TraderId = traderId;
                NameKey = nameKey;
                MinimumLevel = minimumLevel;
                Repeatable = repeatable;
                StartConditionCoverageComplete = startConditionCoverageComplete;
            }

            public string QuestId;
            public string TraderId;
            public string NameKey;
            public int? MinimumLevel;
            public bool Repeatable;
            public bool StartConditionCoverageComplete;
            public HashSet<string> Prerequisites = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> Dependents = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> RequiredTemplates = new HashSet<string>(StringComparer.Ordinal);
            public List<PlannerTopologyPrerequisite> PrerequisiteEdges = new List<PlannerTopologyPrerequisite>();
            public List<PlannerTopologyPrerequisite> DependentEdges = new List<PlannerTopologyPrerequisite>();
        }
    }
}
