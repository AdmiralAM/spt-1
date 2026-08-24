using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerQuestItemRequirement
    {
        public PlannerQuestItemRequirement(
            string questId,
            string conditionId,
            IReadOnlyList<string> templateIds,
            double requiredCount,
            bool foundInRaid,
            string phase)
        {
            QuestId = questId ?? string.Empty;
            ConditionId = conditionId ?? string.Empty;
            TemplateIds = templateIds ?? Array.Empty<string>();
            RequiredCount = requiredCount;
            FoundInRaid = foundInRaid;
            Phase = phase ?? string.Empty;
        }

        public string QuestId { get; private set; }
        public string ConditionId { get; private set; }
        public IReadOnlyList<string> TemplateIds { get; private set; }
        public double RequiredCount { get; private set; }
        public bool FoundInRaid { get; private set; }
        public string Phase { get; private set; }
        public bool IsExactSingleton { get { return TemplateIds.Count == 1; } }
    }

    public sealed class PlannerRequirementIndex
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<PlannerQuestItemRequirement>> byQuest;

        public PlannerRequirementIndex(IReadOnlyDictionary<string, IReadOnlyList<PlannerQuestItemRequirement>> byQuest)
        {
            this.byQuest = byQuest ?? new Dictionary<string, IReadOnlyList<PlannerQuestItemRequirement>>(StringComparer.Ordinal);
        }

        public IReadOnlyList<PlannerQuestItemRequirement> GetQuestRequirements(string questId)
        {
            IReadOnlyList<PlannerQuestItemRequirement> result;
            return !string.IsNullOrWhiteSpace(questId) && byQuest.TryGetValue(questId, out result)
                ? result
                : Array.Empty<PlannerQuestItemRequirement>();
        }
    }

    public static class PlannerRequirementIndexBuilder
    {
        public static PlannerRequirementIndex Build(string topologyJson)
        {
            if (string.IsNullOrWhiteSpace(topologyJson)) throw new ArgumentException("Topology JSON is missing.", "topologyJson");
            object root = Parse(topologyJson);
            Dictionary<string, List<PlannerQuestItemRequirement>> mutable = new Dictionary<string, List<PlannerQuestItemRequirement>>(StringComparer.Ordinal);

            foreach (object node in Values(Get(root, "itemRequirements")))
            {
                string questId = ReadString(Get(node, "questId"));
                if (string.IsNullOrWhiteSpace(questId)) continue;

                List<string> templates = new List<string>();
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (object templateNode in Values(Get(node, "templateIds")))
                {
                    string templateId = ReadString(templateNode);
                    if (!string.IsNullOrWhiteSpace(templateId) && seen.Add(templateId)) templates.Add(templateId);
                }

                if (templates.Count == 0) continue;
                templates.Sort(StringComparer.Ordinal);
                PlannerQuestItemRequirement requirement = new PlannerQuestItemRequirement(
                    questId,
                    ReadString(Get(node, "conditionId")),
                    templates.ToArray(),
                    Math.Max(0d, ReadDouble(Get(node, "requiredCount"), 0d)),
                    ReadBool(Get(node, "foundInRaid"), false),
                    ReadString(Get(node, "phase")));

                List<PlannerQuestItemRequirement> list;
                if (!mutable.TryGetValue(questId, out list))
                {
                    list = new List<PlannerQuestItemRequirement>();
                    mutable[questId] = list;
                }
                list.Add(requirement);
            }

            Dictionary<string, IReadOnlyList<PlannerQuestItemRequirement>> frozen = new Dictionary<string, IReadOnlyList<PlannerQuestItemRequirement>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<PlannerQuestItemRequirement>> pair in mutable)
            {
                pair.Value.Sort((a, b) =>
                {
                    int phase = string.Compare(a.Phase, b.Phase, StringComparison.Ordinal);
                    if (phase != 0) return phase;
                    return string.Compare(a.ConditionId, b.ConditionId, StringComparison.Ordinal);
                });
                frozen[pair.Key] = pair.Value.ToArray();
            }
            return new PlannerRequirementIndex(frozen);
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
        private static double ReadDouble(object token, double fallback)
        {
            double value;
            return double.TryParse(ReadString(token), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

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

    public sealed class PlannerPathItemNeed
    {
        public PlannerPathItemNeed(string templateId, double required, double ownedEligible, double outstanding, bool foundInRaid)
        {
            TemplateId = templateId ?? string.Empty;
            Required = required;
            OwnedEligible = ownedEligible;
            Outstanding = outstanding;
            FoundInRaid = foundInRaid;
        }

        public string TemplateId { get; private set; }
        public double Required { get; private set; }
        public double OwnedEligible { get; private set; }
        public double Outstanding { get; private set; }
        public bool FoundInRaid { get; private set; }
    }

    public sealed class PlannerTemplateAllocation
    {
        public PlannerTemplateAllocation(string templateId, double allocated)
        {
            TemplateId = templateId ?? string.Empty;
            Allocated = Math.Max(0d, allocated);
        }

        public string TemplateId { get; private set; }
        public double Allocated { get; private set; }
    }

    public sealed class PlannerAlternativeItemNeed
    {
        public PlannerAlternativeItemNeed(
            PlannerQuestItemRequirement requirement,
            double ownedAllocated,
            double outstanding,
            IReadOnlyList<PlannerTemplateAllocation> allocations)
        {
            Requirement = requirement ?? throw new ArgumentNullException("requirement");
            OwnedAllocated = Math.Max(0d, ownedAllocated);
            Outstanding = Math.Max(0d, outstanding);
            Allocations = allocations ?? Array.Empty<PlannerTemplateAllocation>();
        }

        public PlannerQuestItemRequirement Requirement { get; private set; }
        public double OwnedAllocated { get; private set; }
        public double Outstanding { get; private set; }
        public IReadOnlyList<PlannerTemplateAllocation> Allocations { get; private set; }
        public bool IsSatisfied { get { return Outstanding <= 0.000001d; } }
    }

    public sealed class PlannerPathItemPlan
    {
        public PlannerPathItemPlan(
            IReadOnlyList<PlannerPathItemNeed> exactNeeds,
            IReadOnlyList<PlannerAlternativeItemNeed> alternativeNeeds)
        {
            ExactNeeds = exactNeeds ?? Array.Empty<PlannerPathItemNeed>();
            AlternativeNeeds = alternativeNeeds ?? Array.Empty<PlannerAlternativeItemNeed>();
        }

        public IReadOnlyList<PlannerPathItemNeed> ExactNeeds { get; private set; }
        public IReadOnlyList<PlannerAlternativeItemNeed> AlternativeNeeds { get; private set; }
        public bool IsExact { get { return true; } }
        public bool IsFullyOwned
        {
            get
            {
                for (int i = 0; i < ExactNeeds.Count; i++) if (ExactNeeds[i].Outstanding > 0.000001d) return false;
                for (int i = 0; i < AlternativeNeeds.Count; i++) if (!AlternativeNeeds[i].IsSatisfied) return false;
                return true;
            }
        }
    }
}
