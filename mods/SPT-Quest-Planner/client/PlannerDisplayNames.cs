using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public static class PlannerDisplayNames
    {
        private static readonly IReadOnlyDictionary<string, string> LocationNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bigmap"] = "Customs",
                ["customs"] = "Customs",
                ["woods"] = "Woods",
                ["shoreline"] = "Shoreline",
                ["rezervbase"] = "Reserve",
                ["reserve"] = "Reserve",
                ["interchange"] = "Interchange",
                ["factory4_day"] = "Factory",
                ["factory4_night"] = "Factory",
                ["factory"] = "Factory",
                ["laboratory"] = "The Lab",
                ["lab"] = "The Lab",
                ["lighthouse"] = "Lighthouse",
                ["tarkovstreets"] = "Streets of Tarkov",
                ["streets"] = "Streets of Tarkov",
                ["sandbox"] = "Ground Zero",
                ["sandbox_high"] = "Ground Zero",
                ["groundzero"] = "Ground Zero"
            };

        public static string Location(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId)) return "Unknown location";
            string value;
            return LocationNames.TryGetValue(locationId.Trim(), out value) ? value : locationId.Trim();
        }

        public static string Objective(PlannerRaidObjectiveKind kind)
        {
            switch (kind)
            {
                case PlannerRaidObjectiveKind.Kill: return "Kill";
                case PlannerRaidObjectiveKind.Visit: return "Visit";
                case PlannerRaidObjectiveKind.Plant: return "Plant / Mark";
                case PlannerRaidObjectiveKind.Find: return "Find / Retrieve";
                case PlannerRaidObjectiveKind.Extract: return "Extract / Survive";
                case PlannerRaidObjectiveKind.Bring: return "Bring / Handover";
                default: return "Other objective";
            }
        }
    }
}
