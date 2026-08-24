using System;
using System.Collections.Generic;
using System.Reflection;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerQuestClientState
    {
        public PlannerQuestClientState(string questId, int disposition, int profileState, bool levelGateSatisfied, bool prerequisitesSatisfied)
        {
            QuestId = questId ?? string.Empty;
            Disposition = disposition;
            ProfileState = profileState;
            LevelGateSatisfied = levelGateSatisfied;
            PrerequisitesSatisfied = prerequisitesSatisfied;
        }

        public string QuestId { get; private set; }
        public int Disposition { get; private set; }
        public int ProfileState { get; private set; }
        public bool LevelGateSatisfied { get; private set; }
        public bool PrerequisitesSatisfied { get; private set; }
    }

    public sealed class PlannerItemClientState
    {
        public PlannerItemClientState(
            string templateId,
            double currentRequired,
            double futureRequired,
            double ownedTotal,
            double ownedFoundInRaid,
            double currentOutstanding,
            double futureOutstandingAfterCurrent)
        {
            TemplateId = templateId ?? string.Empty;
            CurrentRequired = currentRequired;
            FutureRequired = futureRequired;
            OwnedTotal = ownedTotal;
            OwnedFoundInRaid = ownedFoundInRaid;
            CurrentOutstanding = currentOutstanding;
            FutureOutstandingAfterCurrent = futureOutstandingAfterCurrent;
        }

        public string TemplateId { get; private set; }
        public double CurrentRequired { get; private set; }
        public double FutureRequired { get; private set; }
        public double OwnedTotal { get; private set; }
        public double OwnedFoundInRaid { get; private set; }
        public double CurrentOutstanding { get; private set; }
        public double FutureOutstandingAfterCurrent { get; private set; }
    }

    public sealed class PlannerConditionProgress
    {
        public PlannerConditionProgress(string counterId, string type, double value, string sourceQuestId)
        {
            CounterId = counterId ?? string.Empty;
            Type = type;
            Value = value;
            SourceQuestId = sourceQuestId;
        }

        public string CounterId { get; private set; }
        public string Type { get; private set; }
        public double Value { get; private set; }
        public string SourceQuestId { get; private set; }
    }

    public sealed class PlannerClientIndex
    {
        public PlannerClientIndex(
            long generatedAtUnixSeconds,
            IReadOnlyDictionary<string, PlannerQuestClientState> quests,
            IReadOnlyDictionary<string, PlannerItemClientState> items,
            IReadOnlyDictionary<string, PlannerConditionProgress> conditionProgress = null)
        {
            GeneratedAtUnixSeconds = generatedAtUnixSeconds;
            Quests = quests ?? new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            Items = items ?? new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal);
            ConditionProgress = conditionProgress ?? new Dictionary<string, PlannerConditionProgress>(StringComparer.Ordinal);
        }

        public long GeneratedAtUnixSeconds { get; private set; }
        public IReadOnlyDictionary<string, PlannerQuestClientState> Quests { get; private set; }
        public IReadOnlyDictionary<string, PlannerItemClientState> Items { get; private set; }
        public IReadOnlyDictionary<string, PlannerConditionProgress> ConditionProgress { get; private set; }

        public PlannerQuestClientState GetQuest(string questId)
        {
            PlannerQuestClientState value;
            return !string.IsNullOrWhiteSpace(questId) && Quests.TryGetValue(questId, out value) ? value : null;
        }

        public PlannerItemClientState GetItem(string templateId)
        {
            PlannerItemClientState value;
            return !string.IsNullOrWhiteSpace(templateId) && Items.TryGetValue(templateId, out value) ? value : null;
        }

        public PlannerConditionProgress GetConditionProgress(string counterId)
        {
            PlannerConditionProgress value;
            return !string.IsNullOrWhiteSpace(counterId) && ConditionProgress.TryGetValue(counterId, out value) ? value : null;
        }
    }

    public static class PlannerClientIndexBuilder
    {
        public static PlannerClientIndex Build(string stateJson)
        {
            if (string.IsNullOrWhiteSpace(stateJson)) throw new ArgumentException("State JSON is missing.", "stateJson");
            Type tokenType = FindType("Newtonsoft.Json.Linq.JToken");
            if (tokenType == null) throw new InvalidOperationException("Newtonsoft JSON runtime is unavailable.");
            MethodInfo parse = tokenType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (parse == null) throw new InvalidOperationException("Newtonsoft JToken.Parse is unavailable.");
            object root = parse.Invoke(null, new object[] { stateJson });

            long generated = ReadLong(Get(root, "generatedAtUnixSeconds"), 0L);
            Dictionary<string, PlannerQuestClientState> quests = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal);
            object evaluation = Get(root, "evaluation");
            object questMap = Get(evaluation, "quests");
            foreach (KeyValuePair<string, object> entry in Properties(questMap))
            {
                object node = entry.Value;
                string questId = ReadString(Get(node, "questId"));
                if (string.IsNullOrWhiteSpace(questId)) questId = entry.Key;
                if (string.IsNullOrWhiteSpace(questId)) continue;
                quests[questId] = new PlannerQuestClientState(
                    questId,
                    ReadInt(Get(node, "disposition"), 0),
                    ReadInt(Get(node, "profileState"), 0),
                    ReadBool(Get(node, "levelGateSatisfied"), false),
                    ReadBool(Get(node, "prerequisitesSatisfied"), false));
            }

            Dictionary<string, PlannerItemClientState> items = new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal);
            object outstanding = Get(root, "outstandingItems");
            foreach (object node in Values(outstanding))
            {
                string templateId = ReadString(Get(node, "templateId"));
                if (string.IsNullOrWhiteSpace(templateId)) continue;
                items[templateId] = new PlannerItemClientState(
                    templateId,
                    ReadDouble(Get(node, "currentRequired"), 0d),
                    ReadDouble(Get(node, "futureRequired"), 0d),
                    ReadDouble(Get(node, "ownedTotal"), 0d),
                    ReadDouble(Get(node, "ownedFoundInRaid"), 0d),
                    ReadDouble(Get(node, "currentOutstanding"), 0d),
                    ReadDouble(Get(node, "futureOutstandingAfterCurrent"), 0d));
            }

            Dictionary<string, PlannerConditionProgress> progress = new Dictionary<string, PlannerConditionProgress>(StringComparer.Ordinal);
            object player = Get(root, "player");
            object counters = Get(player, "taskConditionCounters");
            foreach (KeyValuePair<string, object> entry in Properties(counters))
            {
                object node = entry.Value;
                string counterId = ReadString(Get(node, "counterId"));
                if (string.IsNullOrWhiteSpace(counterId)) counterId = entry.Key;
                if (string.IsNullOrWhiteSpace(counterId)) continue;
                progress[counterId] = new PlannerConditionProgress(
                    counterId,
                    ReadString(Get(node, "type")),
                    ReadDouble(Get(node, "value"), 0d),
                    ReadString(Get(node, "sourceQuestId")));
            }

            return new PlannerClientIndex(generated, quests, items, progress);
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

        private static IEnumerable<KeyValuePair<string, object>> Properties(object token)
        {
            if (token == null) yield break;
            MethodInfo properties = token.GetType().GetMethod("Properties", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (properties == null) yield break;
            System.Collections.IEnumerable sequence = properties.Invoke(token, null) as System.Collections.IEnumerable;
            if (sequence == null) yield break;
            foreach (object property in sequence)
            {
                if (property == null) continue;
                PropertyInfo name = property.GetType().GetProperty("Name");
                PropertyInfo value = property.GetType().GetProperty("Value");
                string key = name == null ? null : name.GetValue(property, null) as string;
                object item = value == null ? null : value.GetValue(property, null);
                if (!string.IsNullOrWhiteSpace(key)) yield return new KeyValuePair<string, object>(key, item);
            }
        }

        private static IEnumerable<object> Values(object token)
        {
            if (token == null) yield break;
            System.Collections.IEnumerable sequence = token as System.Collections.IEnumerable;
            if (sequence == null) yield break;
            foreach (object value in sequence) yield return value;
        }

        private static string ReadString(object token) { return token == null ? null : token.ToString(); }
        private static int ReadInt(object token, int fallback) { int value; return int.TryParse(ReadString(token), out value) ? value : fallback; }
        private static long ReadLong(object token, long fallback) { long value; return long.TryParse(ReadString(token), out value) ? value : fallback; }
        private static double ReadDouble(object token, double fallback) { double value; return double.TryParse(ReadString(token), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : fallback; }
        private static bool ReadBool(object token, bool fallback) { bool value; return bool.TryParse(ReadString(token), out value) ? value : fallback; }

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
    }
}
