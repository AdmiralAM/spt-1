using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public static class PlannerDisplayNames
    {
        private static readonly IReadOnlyDictionary<string, string> LocationNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PlannerRaidOpportunityBuilder.AnyLocationId] = "Any location",
                ["55f2d3fd4bdc2d5f408b4567"] = "Factory (Day)",
                ["59fc81d786f774390775787e"] = "Factory (Night)",
                ["56f40101d2720b2a4d8b45d6"] = "Customs",
                ["5704e3c2d2720bac5b8b4567"] = "Woods",
                ["5704e4dad2720bb55b8b4567"] = "Lighthouse",
                ["5704e554d2720bac5b8b456e"] = "Shoreline",
                ["5704e5fad2720bc05b8b4567"] = "Reserve",
                ["5714dbc024597771384a510d"] = "Interchange",
                ["5b0fc42d86f7744a585f9105"] = "The Lab",
                ["5714dc692459777137212e12"] = "Streets of Tarkov",
                ["653e6760052c01c1c805532f"] = "Ground Zero",
                ["6733700029c367a3d40b02af"] = "Labyrinth",
                ["bigmap"] = "Customs",
                ["customs"] = "Customs",
                ["woods"] = "Woods",
                ["shoreline"] = "Shoreline",
                ["rezervbase"] = "Reserve",
                ["reserve"] = "Reserve",
                ["interchange"] = "Interchange",
                ["factory4_day"] = "Factory (Day)",
                ["factory4_night"] = "Factory (Night)",
                ["factory"] = "Factory",
                ["laboratory"] = "The Lab",
                ["lab"] = "The Lab",
                ["lighthouse"] = "Lighthouse",
                ["tarkovstreets"] = "Streets of Tarkov",
                ["streets"] = "Streets of Tarkov",
                ["sandbox"] = "Ground Zero (Level ≤ 20)",
                ["sandbox_high"] = "Ground Zero (Level > 20)",
                ["groundzero"] = "Ground Zero",
                ["labyrinth"] = "Labyrinth"
            };

        private static readonly IReadOnlyDictionary<string, string> TargetNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AnyPmc"] = "PMC",
                ["Pmc"] = "PMC",
                ["Usec"] = "USEC",
                ["Bear"] = "BEAR",
                ["Savage"] = "Scav",
                ["Scav"] = "Scav",
                ["AnyScav"] = "Scav",
                ["Boss"] = "Boss",
                ["Any"] = "Any target",
                ["All"] = "Any target"
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
                default: return "Objective";
            }
        }

        public static string Target(string target, PlannerLocaleIndex locale = null)
        {
            if (string.IsNullOrWhiteSpace(target)) return string.Empty;
            string normalized = target.Trim();
            string value;
            if (TargetNames.TryGetValue(normalized, out value)) return value;
            if (LocationNames.TryGetValue(normalized, out value)) return value;

            if (locale != null)
            {
                string item = locale.ItemName(normalized);
                if (!string.IsNullOrWhiteSpace(item) && !string.Equals(item, normalized, StringComparison.Ordinal))
                    return item;
            }

            if (LooksLikeRuntimeType(normalized)) return string.Empty;
            return normalized;
        }

        public static string ObjectiveAction(PlannerRaidObjective objective, PlannerLocaleIndex locale = null)
        {
            if (objective == null) return "Objective";
            string action = Objective(objective.Kind);
            if (objective.Targets == null || objective.Targets.Count == 0) return action;

            List<string> labels = new List<string>(objective.Targets.Count);
            for (int i = 0; i < objective.Targets.Count; i++)
            {
                string label = Target(objective.Targets[i], locale);
                if (!string.IsNullOrWhiteSpace(label) && !labels.Contains(label)) labels.Add(label);
            }
            return labels.Count == 0 ? action : action + " " + string.Join(", ", labels.ToArray());
        }

        private static bool LooksLikeRuntimeType(string value)
        {
            return value.IndexOf("SPTarkov.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("System.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("ListOrT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf('`') >= 0;
        }
    }
}
