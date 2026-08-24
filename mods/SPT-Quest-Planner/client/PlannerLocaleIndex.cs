using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerLocaleIndex
    {
        public PlannerLocaleIndex(string locale, IReadOnlyDictionary<string, string> questNames, IReadOnlyDictionary<string, string> itemNames)
        {
            Locale = string.IsNullOrWhiteSpace(locale) ? "en" : locale.Trim();
            QuestNames = questNames ?? new Dictionary<string, string>(StringComparer.Ordinal);
            ItemNames = itemNames ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public string Locale { get; private set; }
        public IReadOnlyDictionary<string, string> QuestNames { get; private set; }
        public IReadOnlyDictionary<string, string> ItemNames { get; private set; }

        public string QuestName(string questId)
        {
            string value;
            return !string.IsNullOrWhiteSpace(questId) && QuestNames.TryGetValue(questId, out value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : questId ?? string.Empty;
        }

        public string ItemName(string templateId)
        {
            string value;
            return !string.IsNullOrWhiteSpace(templateId) && ItemNames.TryGetValue(templateId, out value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : templateId ?? string.Empty;
        }
    }

    public static class PlannerLocaleIndexBuilder
    {
        public static PlannerLocaleIndex Build(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Locale payload JSON is missing.", "json");
            object root = Parse(json);
            int schema = ReadInt(Get(root, "schemaVersion"), 0);
            if (schema != PlannerClientContract.SchemaVersion)
                throw new InvalidOperationException("Unsupported Quest Planner locale schema " + schema + ".");

            string locale = ReadString(Get(root, "locale"));
            Dictionary<string, string> quests = ReadDictionary(Get(root, "questNames"));
            Dictionary<string, string> items = ReadDictionary(Get(root, "itemNames"));
            return new PlannerLocaleIndex(locale, quests, items);
        }

        private static Dictionary<string, string> ReadDictionary(object token)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (token == null) return result;
            IEnumerable sequence = token as IEnumerable;
            if (sequence == null) return result;

            foreach (object entry in sequence)
            {
                if (entry == null) continue;

                Type entryType = entry.GetType();
                PropertyInfo nameProperty = entryType.GetProperty("Name");
                PropertyInfo keyProperty = entryType.GetProperty("Key");
                PropertyInfo valueProperty = entryType.GetProperty("Value");
                if (valueProperty == null || (nameProperty == null && keyProperty == null)) continue;

                object keyObject = nameProperty != null ? nameProperty.GetValue(entry, null) : keyProperty.GetValue(entry, null);
                object valueObject = valueProperty.GetValue(entry, null);
                string key = keyObject == null ? null : keyObject.ToString();
                string value = valueObject == null ? null : valueObject.ToString();
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value)) result[key] = value;
            }
            return result;
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
            if (stringItem != null) { try { return stringItem.GetValue(token, new object[] { name }); } catch { } }
            PropertyInfo objectItem = token.GetType().GetProperty("Item", new[] { typeof(object) });
            if (objectItem != null) { try { return objectItem.GetValue(token, new object[] { name }); } catch { } }
            return null;
        }

        private static int ReadInt(object token, int fallback) { int value; return int.TryParse(ReadString(token), out value) ? value : fallback; }
        private static string ReadString(object token) { return token == null ? null : token.ToString(); }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try { Type type = assemblies[i].GetType(fullName, false, false); if (type != null) return type; }
                catch { }
            }
            return null;
        }
    }
}
