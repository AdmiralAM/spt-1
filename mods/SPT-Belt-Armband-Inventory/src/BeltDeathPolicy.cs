using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTBeltArmbandInventory
{
    public readonly struct BeltInventoryNode
    {
        public BeltInventoryNode(string id, string parentId, string slotId, string templateId = null)
        {
            Id = id;
            ParentId = parentId;
            SlotId = slotId;
            TemplateId = templateId;
        }

        public string Id { get; }
        public string ParentId { get; }
        public string SlotId { get; }
        public string TemplateId { get; }
    }

    public readonly struct ProtectedWearableRoot
    {
        public ProtectedWearableRoot(string slotId, string templateId)
        {
            SlotId = slotId;
            TemplateId = templateId;
        }

        public string SlotId { get; }
        public string TemplateId { get; }
    }

    public static class BeltDeathPolicy
    {
        public const string ArmBand = "ArmBand";

        // Legacy pure-policy overloads remain for historical regression coverage.
        // Production server patches use explicit slot/template roots below so an
        // ordinary ArmBand or an unrelated item in pseudo-slots 15/16 never gains
        // B&A&HB protection accidentally.
        public static HashSet<string> GetKeptTreeIds(IEnumerable<BeltInventoryNode> nodes)
        {
            return GetKeptTreeIdsCore(nodes, null, false);
        }

        public static bool ShouldKeep(string itemId, IEnumerable<BeltInventoryNode> nodes)
        {
            return !string.IsNullOrEmpty(itemId) && GetKeptTreeIds(nodes).Contains(itemId);
        }

        public static string[] FilterLostInsuredIds(IEnumerable<string> lostIds, IEnumerable<BeltInventoryNode> nodes)
        {
            var kept = GetKeptTreeIds(nodes);
            if (kept.Count == 0) return lostIds == null ? Array.Empty<string>() : lostIds.ToArray();
            return (lostIds ?? Array.Empty<string>()).Where(id => !kept.Contains(id)).ToArray();
        }

        public static HashSet<string> GetKeptTreeIds(IEnumerable<BeltInventoryNode> nodes, string protectedRootTemplateId)
        {
            return GetKeptTreeIdsCore(nodes, protectedRootTemplateId, true);
        }

        public static bool ShouldKeep(string itemId, IEnumerable<BeltInventoryNode> nodes, string protectedRootTemplateId)
        {
            return !string.IsNullOrEmpty(itemId)
                && GetKeptTreeIds(nodes, protectedRootTemplateId).Contains(itemId);
        }

        public static string[] FilterLostInsuredIds(IEnumerable<string> lostIds, IEnumerable<BeltInventoryNode> nodes, string protectedRootTemplateId)
        {
            var kept = GetKeptTreeIds(nodes, protectedRootTemplateId);
            if (kept.Count == 0) return lostIds == null ? Array.Empty<string>() : lostIds.ToArray();
            return (lostIds ?? Array.Empty<string>()).Where(id => !kept.Contains(id)).ToArray();
        }

        public static HashSet<string> GetKeptTreeIds(
            IEnumerable<BeltInventoryNode> nodes,
            IEnumerable<ProtectedWearableRoot> protectedRoots)
        {
            BeltInventoryNode[] items = nodes == null ? Array.Empty<BeltInventoryNode>() : nodes.ToArray();
            ProtectedWearableRoot[] roots = protectedRoots == null ? Array.Empty<ProtectedWearableRoot>() : protectedRoots.ToArray();
            var rootIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < roots.Length; i++)
            {
                if (string.IsNullOrEmpty(roots[i].SlotId) || string.IsNullOrEmpty(roots[i].TemplateId))
                    continue;

                for (int j = 0; j < items.Length; j++)
                {
                    if (!string.Equals(items[j].SlotId, roots[i].SlotId, StringComparison.Ordinal)
                        || !string.Equals(items[j].TemplateId, roots[i].TemplateId, StringComparison.Ordinal)
                        || string.IsNullOrEmpty(items[j].Id))
                        continue;
                    rootIds.Add(items[j].Id);
                }
            }

            if (rootIds.Count == 0) return rootIds;
            return ExpandTrees(items, rootIds);
        }

        public static bool ShouldKeep(
            string itemId,
            IEnumerable<BeltInventoryNode> nodes,
            IEnumerable<ProtectedWearableRoot> protectedRoots)
        {
            return !string.IsNullOrEmpty(itemId)
                && GetKeptTreeIds(nodes, protectedRoots).Contains(itemId);
        }

        public static string[] FilterLostInsuredIds(
            IEnumerable<string> lostIds,
            IEnumerable<BeltInventoryNode> nodes,
            IEnumerable<ProtectedWearableRoot> protectedRoots)
        {
            var kept = GetKeptTreeIds(nodes, protectedRoots);
            if (kept.Count == 0) return lostIds == null ? Array.Empty<string>() : lostIds.ToArray();
            return (lostIds ?? Array.Empty<string>()).Where(id => !kept.Contains(id)).ToArray();
        }

        static HashSet<string> GetKeptTreeIdsCore(IEnumerable<BeltInventoryNode> nodes, string protectedRootTemplateId, bool requireTemplateMatch)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (requireTemplateMatch && string.IsNullOrEmpty(protectedRootTemplateId)) return result;

            BeltInventoryNode[] items = nodes == null ? Array.Empty<BeltInventoryNode>() : nodes.ToArray();
            BeltInventoryNode? belt = null;
            for (int i = 0; i < items.Length; i++)
            {
                if (!string.Equals(items[i].SlotId, ArmBand, StringComparison.Ordinal)) continue;
                if (requireTemplateMatch && !string.Equals(items[i].TemplateId, protectedRootTemplateId, StringComparison.Ordinal)) continue;
                belt = items[i];
                break;
            }

            if (!belt.HasValue || string.IsNullOrEmpty(belt.Value.Id)) return result;
            result.Add(belt.Value.Id);
            return ExpandTrees(items, result);
        }

        static HashSet<string> ExpandTrees(BeltInventoryNode[] items, HashSet<string> rootIds)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < items.Length; i++)
            {
                string parentId = items[i].ParentId;
                string id = items[i].Id;
                if (string.IsNullOrEmpty(parentId) || string.IsNullOrEmpty(id)) continue;
                if (!children.TryGetValue(parentId, out var list))
                {
                    list = new List<string>();
                    children[parentId] = list;
                }
                list.Add(id);
            }

            var pending = new Stack<string>();
            foreach (string rootId in rootIds) pending.Push(rootId);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (!result.Add(current)) continue;
                if (!children.TryGetValue(current, out var directChildren)) continue;
                for (int i = 0; i < directChildren.Count; i++) pending.Push(directChildren[i]);
            }

            return result;
        }
    }
}
