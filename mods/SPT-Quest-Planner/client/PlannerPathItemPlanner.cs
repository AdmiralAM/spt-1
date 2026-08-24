using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerPathItemPlanner
    {
        private readonly PlannerQueryEngine query;
        private readonly PlannerRequirementIndex requirements;
        private readonly PlannerClientIndex state;

        public PlannerPathItemPlanner(PlannerQueryEngine query, PlannerRequirementIndex requirements, PlannerClientIndex state)
        {
            this.query = query ?? throw new ArgumentNullException("query");
            this.requirements = requirements ?? throw new ArgumentNullException("requirements");
            this.state = state ?? throw new ArgumentNullException("state");
        }

        public PlannerPathItemPlan BuildForTarget(string targetQuestId)
        {
            IReadOnlyList<string> path = query.GetIncompletePrerequisitePlan(targetQuestId);
            Dictionary<string, MutableNeed> byTemplate = new Dictionary<string, MutableNeed>(StringComparer.Ordinal);
            List<PlannerQuestItemRequirement> unresolved = new List<PlannerQuestItemRequirement>();

            for (int i = 0; i < path.Count; i++)
            {
                IReadOnlyList<PlannerQuestItemRequirement> questRequirements = requirements.GetQuestRequirements(path[i]);
                for (int r = 0; r < questRequirements.Count; r++)
                {
                    PlannerQuestItemRequirement requirement = questRequirements[r];
                    if (!requirement.IsExactSingleton)
                    {
                        unresolved.Add(requirement);
                        continue;
                    }

                    string templateId = requirement.TemplateIds[0];
                    MutableNeed need;
                    if (!byTemplate.TryGetValue(templateId, out need))
                    {
                        need = new MutableNeed();
                        byTemplate[templateId] = need;
                    }

                    if (requirement.FoundInRaid) need.FirRequired += requirement.RequiredCount;
                    else need.GenericRequired += requirement.RequiredCount;
                }
            }

            List<PlannerPathItemNeed> exact = new List<PlannerPathItemNeed>(byTemplate.Count * 2);
            List<string> templateIds = new List<string>(byTemplate.Keys);
            templateIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < templateIds.Count; i++)
            {
                string templateId = templateIds[i];
                MutableNeed need = byTemplate[templateId];
                PlannerItemClientState owned = state.GetItem(templateId);
                double total = owned == null ? 0d : Math.Max(0d, owned.OwnedTotal);
                double fir = owned == null ? 0d : Math.Max(0d, Math.Min(owned.OwnedFoundInRaid, total));
                double nonFir = Math.Max(0d, total - fir);

                double firUsed = Math.Min(fir, need.FirRequired);
                double firOutstanding = Math.Max(0d, need.FirRequired - firUsed);
                double firRemaining = Math.Max(0d, fir - firUsed);

                double genericFromNonFir = Math.Min(nonFir, need.GenericRequired);
                double genericRemaining = Math.Max(0d, need.GenericRequired - genericFromNonFir);
                double genericFromFir = Math.Min(firRemaining, genericRemaining);
                double genericOutstanding = Math.Max(0d, genericRemaining - genericFromFir);

                if (need.FirRequired > 0d)
                    exact.Add(new PlannerPathItemNeed(templateId, need.FirRequired, firUsed, firOutstanding, true));
                if (need.GenericRequired > 0d)
                    exact.Add(new PlannerPathItemNeed(templateId, need.GenericRequired, genericFromNonFir + genericFromFir, genericOutstanding, false));
            }

            return new PlannerPathItemPlan(exact.ToArray(), unresolved.ToArray());
        }

        private sealed class MutableNeed
        {
            public double FirRequired;
            public double GenericRequired;
        }
    }
}
