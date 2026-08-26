using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTBeltArmbandInventory
{
    public readonly struct BeltInventoryNode
    {
        public BeltInventoryNode(string id, string? parentId, string? slotId, string? templateId = null)
        {
            Id = id;
            ParentId = parentId;
            SlotId = slotId;
            TemplateId = templateId;
        }

        public string Id { get; }
        public string? ParentId { get; }
        public string? SlotId { get; }
        public string? TemplateId { get; }
    }

    public static class BeltDeathPolicy
    {
        public const string ArmBand = "ArmBand";

        public static HashSet<string> GetKeptTreeIds(IEnumerable<BeltInventoryNode>? nodes, string? protectedRootTemplateId)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(protectedRootTemplateId)) return result;

            BeltInventoryNode[] items = nodes == null ? Array.Empty<BeltInventoryNode>() : nodes.ToArray();
            BeltInventoryNode? belt = null;
            for (int i = 0; i < items.Length; i++)
            {
                if (string.Equals(items[i].SlotId, ArmBand, StringComparison.Ordinal)
                    && string.Equals(items[i].TemplateId, protectedRootTemplateId, StringComparison.Ordinal))
                {
                    belt = items[i];
                    break;
                }
            }

            if (!belt.HasValue || string.IsNullOrEmpty(belt.Value.Id)) return result;

            var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int i = 0; i < items.Length; i++)
            {
                string? parentId = items[i].ParentId;
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
            pending.Push(belt.Value.Id);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (!result.Add(current)) continue;
                if (!children.TryGetValue(current, out var directChildren)) continue;
                for (int i = 0; i < directChildren.Count; i++) pending.Push(directChildren[i]);
            }

            return result;
        }

        public static bool ShouldKeep(string? itemId, IEnumerable<BeltInventoryNode>? nodes, string? protectedRootTemplateId)
        {
            return !string.IsNullOrEmpty(itemId)
                && GetKeptTreeIds(nodes, protectedRootTemplateId).Contains(itemId);
        }

        public static string[] FilterLostInsuredIds(IEnumerable<string>? lostIds, IEnumerable<BeltInventoryNode>? nodes, string? protectedRootTemplateId)
        {
            var kept = GetKeptTreeIds(nodes, protectedRootTemplateId);
            if (kept.Count == 0) return lostIds == null ? Array.Empty<string>() : lostIds.ToArray();
            return (lostIds ?? Array.Empty<string>()).Where(id => !kept.Contains(id)).ToArray();
        }
    }
}
