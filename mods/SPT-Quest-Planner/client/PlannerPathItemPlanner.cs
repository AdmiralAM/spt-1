using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerPathItemPlanner
    {
        private const int MaxConditions = 512;
        private const int MaxTemplates = 512;
        private const double Epsilon = 0.000001d;

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
            List<PlannerQuestItemRequirement> all = new List<PlannerQuestItemRequirement>();
            for (int i = 0; i < path.Count; i++)
            {
                IReadOnlyList<PlannerQuestItemRequirement> questRequirements = requirements.GetQuestRequirements(path[i]);
                for (int r = 0; r < questRequirements.Count; r++)
                    if (questRequirements[r].RequiredCount > Epsilon) all.Add(questRequirements[r]);
            }

            if (all.Count > MaxConditions)
                throw new InvalidOperationException("Selected quest path exceeds bounded item-condition limit of " + MaxConditions + ".");

            HashSet<string> templateSet = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < all.Count; i++)
                for (int t = 0; t < all[i].TemplateIds.Count; t++)
                    if (!string.IsNullOrWhiteSpace(all[i].TemplateIds[t])) templateSet.Add(all[i].TemplateIds[t]);
            if (templateSet.Count > MaxTemplates)
                throw new InvalidOperationException("Selected quest path exceeds bounded item-template limit of " + MaxTemplates + ".");

            List<string> templates = new List<string>(templateSet);
            templates.Sort(StringComparer.Ordinal);

            Dictionary<string, double> totalByTemplate = new Dictionary<string, double>(StringComparer.Ordinal);
            Dictionary<string, double> firByTemplate = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int i = 0; i < templates.Count; i++)
            {
                PlannerItemClientState owned = state.GetItem(templates[i]);
                double total = owned == null ? 0d : Math.Max(0d, owned.OwnedTotal);
                double fir = owned == null ? 0d : Math.Max(0d, Math.Min(owned.OwnedFoundInRaid, total));
                totalByTemplate[templates[i]] = total;
                firByTemplate[templates[i]] = fir;
            }

            List<PlannerQuestItemRequirement> firRequirements = new List<PlannerQuestItemRequirement>();
            List<PlannerQuestItemRequirement> genericRequirements = new List<PlannerQuestItemRequirement>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].FoundInRaid) firRequirements.Add(all[i]);
                else genericRequirements.Add(all[i]);
            }
            SortRequirements(firRequirements);
            SortRequirements(genericRequirements);

            FlowResult firFlow = Allocate(firRequirements, templates, firByTemplate);
            Dictionary<string, double> genericStock = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int i = 0; i < templates.Count; i++)
            {
                string templateId = templates[i];
                double firUsed;
                firFlow.UsedByTemplate.TryGetValue(templateId, out firUsed);
                genericStock[templateId] = Math.Max(0d, totalByTemplate[templateId] - firUsed);
            }
            FlowResult genericFlow = Allocate(genericRequirements, templates, genericStock);

            Dictionary<string, MutableNeed> exactByTemplate = new Dictionary<string, MutableNeed>(StringComparer.Ordinal);
            List<PlannerAlternativeItemNeed> alternatives = new List<PlannerAlternativeItemNeed>();
            AppendResults(firRequirements, firFlow, true, exactByTemplate, alternatives);
            AppendResults(genericRequirements, genericFlow, false, exactByTemplate, alternatives);

            List<PlannerPathItemNeed> exact = new List<PlannerPathItemNeed>(exactByTemplate.Count * 2);
            List<string> exactTemplates = new List<string>(exactByTemplate.Keys);
            exactTemplates.Sort(StringComparer.Ordinal);
            for (int i = 0; i < exactTemplates.Count; i++)
            {
                string templateId = exactTemplates[i];
                MutableNeed need = exactByTemplate[templateId];
                if (need.FirRequired > Epsilon)
                    exact.Add(new PlannerPathItemNeed(templateId, need.FirRequired, need.FirAllocated, Math.Max(0d, need.FirRequired - need.FirAllocated), true));
                if (need.GenericRequired > Epsilon)
                    exact.Add(new PlannerPathItemNeed(templateId, need.GenericRequired, need.GenericAllocated, Math.Max(0d, need.GenericRequired - need.GenericAllocated), false));
            }

            alternatives.Sort((a, b) => CompareRequirement(a.Requirement, b.Requirement));
            return new PlannerPathItemPlan(exact.ToArray(), alternatives.ToArray());
        }

        private static void AppendResults(
            IReadOnlyList<PlannerQuestItemRequirement> source,
            FlowResult flow,
            bool foundInRaid,
            Dictionary<string, MutableNeed> exactByTemplate,
            List<PlannerAlternativeItemNeed> alternatives)
        {
            for (int i = 0; i < source.Count; i++)
            {
                PlannerQuestItemRequirement requirement = source[i];
                double allocated;
                flow.AllocatedByRequirement.TryGetValue(requirement, out allocated);
                allocated = Math.Min(requirement.RequiredCount, Math.Max(0d, allocated));

                if (requirement.IsExactSingleton)
                {
                    string templateId = requirement.TemplateIds[0];
                    MutableNeed need;
                    if (!exactByTemplate.TryGetValue(templateId, out need))
                    {
                        need = new MutableNeed();
                        exactByTemplate[templateId] = need;
                    }
                    if (foundInRaid)
                    {
                        need.FirRequired += requirement.RequiredCount;
                        need.FirAllocated += allocated;
                    }
                    else
                    {
                        need.GenericRequired += requirement.RequiredCount;
                        need.GenericAllocated += allocated;
                    }
                    continue;
                }

                Dictionary<string, double> byTemplate;
                flow.Allocations.TryGetValue(requirement, out byTemplate);
                List<PlannerTemplateAllocation> allocations = new List<PlannerTemplateAllocation>();
                if (byTemplate != null)
                {
                    List<string> keys = new List<string>(byTemplate.Keys);
                    keys.Sort(StringComparer.Ordinal);
                    for (int t = 0; t < keys.Count; t++)
                        if (byTemplate[keys[t]] > Epsilon) allocations.Add(new PlannerTemplateAllocation(keys[t], byTemplate[keys[t]]));
                }
                alternatives.Add(new PlannerAlternativeItemNeed(
                    requirement,
                    allocated,
                    Math.Max(0d, requirement.RequiredCount - allocated),
                    allocations.ToArray()));
            }
        }

        private static FlowResult Allocate(
            IReadOnlyList<PlannerQuestItemRequirement> requirements,
            IReadOnlyList<string> templates,
            IReadOnlyDictionary<string, double> stock)
        {
            FlowResult result = new FlowResult();
            if (requirements.Count == 0 || templates.Count == 0) return result;

            int source = 0;
            int templateStart = 1;
            int requirementStart = templateStart + templates.Count;
            int sink = requirementStart + requirements.Count;
            int nodeCount = sink + 1;
            double[,] capacity = new double[nodeCount, nodeCount];
            double[,] flow = new double[nodeCount, nodeCount];

            Dictionary<string, int> templateNode = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int t = 0; t < templates.Count; t++)
            {
                string templateId = templates[t];
                templateNode[templateId] = templateStart + t;
                double available;
                stock.TryGetValue(templateId, out available);
                capacity[source, templateStart + t] = Math.Max(0d, available);
            }

            double infinite = 1d;
            for (int r = 0; r < requirements.Count; r++) infinite += Math.Max(0d, requirements[r].RequiredCount);
            for (int r = 0; r < requirements.Count; r++)
            {
                PlannerQuestItemRequirement requirement = requirements[r];
                int requirementNode = requirementStart + r;
                capacity[requirementNode, sink] = Math.Max(0d, requirement.RequiredCount);
                for (int t = 0; t < requirement.TemplateIds.Count; t++)
                {
                    int node;
                    if (templateNode.TryGetValue(requirement.TemplateIds[t], out node)) capacity[node, requirementNode] = infinite;
                }
            }

            int[] parent = new int[nodeCount];
            while (FindAugmentingPath(capacity, flow, source, sink, parent))
            {
                double delta = double.MaxValue;
                for (int v = sink; v != source; v = parent[v])
                {
                    int u = parent[v];
                    delta = Math.Min(delta, capacity[u, v] - flow[u, v]);
                }
                if (delta <= Epsilon || delta == double.MaxValue) break;
                for (int v = sink; v != source; v = parent[v])
                {
                    int u = parent[v];
                    flow[u, v] += delta;
                    flow[v, u] -= delta;
                }
            }

            for (int t = 0; t < templates.Count; t++)
            {
                string templateId = templates[t];
                int tNode = templateStart + t;
                double used = Math.Max(0d, flow[source, tNode]);
                if (used > Epsilon) result.UsedByTemplate[templateId] = used;
                for (int r = 0; r < requirements.Count; r++)
                {
                    double allocated = Math.Max(0d, flow[tNode, requirementStart + r]);
                    if (allocated <= Epsilon) continue;
                    PlannerQuestItemRequirement requirement = requirements[r];
                    Dictionary<string, double> perTemplate;
                    if (!result.Allocations.TryGetValue(requirement, out perTemplate))
                    {
                        perTemplate = new Dictionary<string, double>(StringComparer.Ordinal);
                        result.Allocations[requirement] = perTemplate;
                    }
                    perTemplate[templateId] = allocated;
                    double current;
                    result.AllocatedByRequirement.TryGetValue(requirement, out current);
                    result.AllocatedByRequirement[requirement] = current + allocated;
                }
            }
            return result;
        }

        private static bool FindAugmentingPath(double[,] capacity, double[,] flow, int source, int sink, int[] parent)
        {
            int count = parent.Length;
            for (int i = 0; i < count; i++) parent[i] = -1;
            bool[] visited = new bool[count];
            int[] queue = new int[count];
            int head = 0;
            int tail = 0;
            queue[tail++] = source;
            visited[source] = true;
            while (head < tail)
            {
                int u = queue[head++];
                for (int v = 0; v < count; v++)
                {
                    if (visited[v]) continue;
                    if (capacity[u, v] - flow[u, v] <= Epsilon) continue;
                    visited[v] = true;
                    parent[v] = u;
                    if (v == sink) return true;
                    queue[tail++] = v;
                }
            }
            return false;
        }

        private static void SortRequirements(List<PlannerQuestItemRequirement> values)
        {
            values.Sort(CompareRequirement);
        }

        private static int CompareRequirement(PlannerQuestItemRequirement a, PlannerQuestItemRequirement b)
        {
            int templates = a.TemplateIds.Count.CompareTo(b.TemplateIds.Count);
            if (templates != 0) return templates;
            int quest = string.Compare(a.QuestId, b.QuestId, StringComparison.Ordinal);
            if (quest != 0) return quest;
            int phase = string.Compare(a.Phase, b.Phase, StringComparison.Ordinal);
            if (phase != 0) return phase;
            return string.Compare(a.ConditionId, b.ConditionId, StringComparison.Ordinal);
        }

        private sealed class MutableNeed
        {
            public double FirRequired;
            public double FirAllocated;
            public double GenericRequired;
            public double GenericAllocated;
        }

        private sealed class FlowResult
        {
            public Dictionary<PlannerQuestItemRequirement, double> AllocatedByRequirement { get; } = new Dictionary<PlannerQuestItemRequirement, double>();
            public Dictionary<PlannerQuestItemRequirement, Dictionary<string, double>> Allocations { get; } = new Dictionary<PlannerQuestItemRequirement, Dictionary<string, double>>();
            public Dictionary<string, double> UsedByTemplate { get; } = new Dictionary<string, double>(StringComparer.Ordinal);
        }
    }
}
