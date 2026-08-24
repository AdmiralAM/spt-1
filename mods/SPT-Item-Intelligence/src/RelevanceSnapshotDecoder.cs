using System;
using System.Collections.Generic;

namespace SPTItemIntelligence
{
    public sealed class RelevanceSnapshotDecoder : IRequirementSnapshotDecoder
    {
        readonly IRequirementSnapshotDecoder inner;

        public RelevanceSnapshotDecoder(IRequirementSnapshotDecoder inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public RequirementDataEnvelope Decode(string json)
        {
            RequirementDataEnvelope snapshot = inner.Decode(json);
            Dictionary<string, ItemRelevanceState> staticRelevance = ProjectStatic(snapshot.prices);
            Dictionary<string, int> onYou = ProjectOnYou(snapshot.profile);
            HashSet<string> keys = new HashSet<string>(staticRelevance.Keys, StringComparer.Ordinal);
            keys.UnionWith(onYou.Keys);

            Dictionary<string, ItemRelevanceState> combined = new Dictionary<string, ItemRelevanceState>(StringComparer.Ordinal);
            foreach (string templateId in keys)
            {
                ItemRelevanceState staticState;
                staticRelevance.TryGetValue(templateId, out staticState);
                staticState = staticState ?? ItemRelevanceState.Empty;
                int equipped;
                onYou.TryGetValue(templateId, out equipped);
                combined[templateId] = new ItemRelevanceState(staticState.CraftCount, staticState.BarterCount, equipped);
            }
            ItemRelevanceRegistry.Replace(combined);
            return snapshot;
        }

        static Dictionary<string, ItemRelevanceState> ProjectStatic(object prices)
        {
            Dictionary<string, ItemRelevanceState> result = new Dictionary<string, ItemRelevanceState>(StringComparer.Ordinal);
            foreach (object entry in JsonNode.Values(prices))
            {
                string templateId = RequirementContribution.NormalizeId(JsonNode.ReadString(JsonNode.Get(entry, "templateId", "TemplateId")));
                if (templateId.Length == 0) continue;
                int craftCount = Math.Max(0, JsonNode.ReadInt(JsonNode.Get(entry, "craftCount", "CraftCount"), 0));
                int barterCount = Math.Max(0, JsonNode.ReadInt(JsonNode.Get(entry, "barterCount", "BarterCount"), 0));
                if (craftCount > 0 || barterCount > 0) result[templateId] = new ItemRelevanceState(craftCount, barterCount);
            }
            return result;
        }

        static Dictionary<string, int> ProjectOnYou(object profile)
        {
            Dictionary<string, int> totals = new Dictionary<string, int>(StringComparer.Ordinal);
            object inventory = JsonNode.Get(profile, "Inventory", "inventory");
            string equipmentRoot = JsonNode.ReadString(JsonNode.Get(inventory, "equipment", "Equipment")).Trim();
            if (equipmentRoot.Length == 0) return totals;

            Dictionary<string, InventoryNode> nodes = new Dictionary<string, InventoryNode>(StringComparer.OrdinalIgnoreCase);
            foreach (object item in JsonNode.Values(JsonNode.Get(inventory, "items", "Items")))
            {
                string id = JsonNode.ReadString(JsonNode.Get(item, "_id", "id", "Id")).Trim();
                string templateId = RequirementContribution.NormalizeId(JsonNode.ReadString(JsonNode.Get(item, "_tpl", "tpl", "TemplateId")));
                if (id.Length == 0 || templateId.Length == 0) continue;
                string parentId = JsonNode.ReadString(JsonNode.Get(item, "parentId", "ParentId")).Trim();
                object upd = JsonNode.Get(item, "upd", "Upd");
                int count = Math.Max(1, JsonNode.ReadInt(JsonNode.Get(upd, "StackObjectsCount", "stackObjectsCount"), 1));
                nodes[id] = new InventoryNode(parentId, templateId, count);
            }

            Dictionary<string, bool> memo = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, InventoryNode> pair in nodes)
            {
                if (!IsDescendantOfEquipment(pair.Key, equipmentRoot, nodes, memo)) continue;
                int current;
                totals.TryGetValue(pair.Value.TemplateId, out current);
                long next = (long)current + pair.Value.Count;
                totals[pair.Value.TemplateId] = next > int.MaxValue ? int.MaxValue : (int)next;
            }
            return totals;
        }

        static bool IsDescendantOfEquipment(
            string id,
            string equipmentRoot,
            IDictionary<string, InventoryNode> nodes,
            IDictionary<string, bool> memo)
        {
            bool cached;
            if (memo.TryGetValue(id, out cached)) return cached;
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string current = id;
            while (current.Length > 0 && visited.Add(current))
            {
                InventoryNode node;
                if (!nodes.TryGetValue(current, out node)) break;
                if (string.Equals(node.ParentId, equipmentRoot, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string visitedId in visited) memo[visitedId] = true;
                    return true;
                }
                current = node.ParentId;
            }
            foreach (string visitedId in visited) memo[visitedId] = false;
            return false;
        }

        sealed class InventoryNode
        {
            public InventoryNode(string parentId, string templateId, int count)
            {
                ParentId = parentId ?? string.Empty;
                TemplateId = templateId ?? string.Empty;
                Count = Math.Max(1, count);
            }
            public string ParentId { get; }
            public string TemplateId { get; }
            public int Count { get; }
        }
    }
}
