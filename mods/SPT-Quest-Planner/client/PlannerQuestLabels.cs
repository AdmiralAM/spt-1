using System;

namespace SPTQuestPlanner.Client
{
    public static class PlannerQuestLabels
    {
        public static string Resolve(PlannerTopologyIndex topology, string questId)
        {
            string fallback = string.IsNullOrWhiteSpace(questId) ? "Unknown quest" : questId.Trim();
            if (topology == null || string.IsNullOrWhiteSpace(questId)) return fallback;

            PlannerTopologyQuest quest = topology.GetQuest(questId);
            if (quest == null || string.IsNullOrWhiteSpace(quest.NameKey)) return fallback;

            string name = quest.NameKey.Trim();
            return IsTechnicalLocaleKey(name, questId) ? fallback : name;
        }

        private static bool IsTechnicalLocaleKey(string value, string questId)
        {
            if (string.Equals(value, questId, StringComparison.OrdinalIgnoreCase)) return true;
            if (value.Length >= 24 && value.IndexOf(' ') < 0 && value.IndexOf('-') < 0) return true;
            return false;
        }
    }
}
