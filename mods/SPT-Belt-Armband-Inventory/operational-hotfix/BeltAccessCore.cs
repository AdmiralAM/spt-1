using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BAndHB.OperationalAccess
{
    // Shared runtime/test policy. This code never writes to inventory or profile data.
    public static class BeltAccessCore
    {
        public const int MaxDepth = 8;
        public const int MaxItems = 256;
        public const int MaxEdges = 1024;
        public sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
        public static bool TryCollect(object root, Func<object, IEnumerable> children, out List<object> result)
        {
            result = new List<object>();
            if (root == null || children == null) return false;
            var seen = new HashSet<object>(ReferenceComparer.Instance) { root };
            var queue = new Queue<KeyValuePair<object, int>>();
            queue.Enqueue(new KeyValuePair<object, int>(root, 0));
            int edges = 0;
            try
            {
                while (queue.Count != 0)
                {
                    var node = queue.Dequeue();
                    var items = children(node.Key);
                    if (items == null) continue;
                    foreach (object child in items)
                    {
                        if (++edges > MaxEdges) { result.Clear(); return false; }
                        if (child == null) continue;
                        if (node.Value >= MaxDepth || !seen.Add(child) || seen.Count > MaxItems + 1)
                        { result.Clear(); return false; }
                        result.Add(child);
                        queue.Enqueue(new KeyValuePair<object, int>(child, node.Value + 1));
                    }
                }
                return true;
            }
            catch { result.Clear(); return false; }
        }
        public static IEnumerable<T> Merge<T>(IEnumerable<T> original, IList<object> extra) where T : class
        {
            if (original == null || extra == null || extra.Count == 0) return original;
            // Materialize exactly once, including when every extra candidate is already present.
            var merged = new List<T>();
            var seen = new HashSet<object>(ReferenceComparer.Instance);
            foreach (T item in original) { merged.Add(item); if (item != null) seen.Add(item); }
            foreach (object item in extra) if (item is T typed && seen.Add(item)) merged.Add(typed);
            return merged;
        }
        public static bool IsGridDescendant(object item, object root, Func<object, IEnumerable> parents, Func<object, IEnumerable> children)
        {
            if (item == null || root == null || ReferenceEquals(item, root)) return false;
            try
            {
                object previous = item;
                int depth = 0; int parentSteps = 0;
                foreach (object parent in parents(item))
                {
                    if (++parentSteps > MaxDepth + 1) return false;
                    if (ReferenceEquals(parent, item) && depth == 0) continue;
                    if (parent == null || ++depth > MaxDepth) return false;
                    bool linked = false; int count = 0;
                    foreach (object child in children(parent))
                    {
                        if (++count > MaxEdges) return false;
                        if (ReferenceEquals(child, previous)) { linked = true; break; }
                    }
                    if (!linked) return false;
                    if (ReferenceEquals(parent, root)) return true;
                    previous = parent;
                }
            }
            catch { }
            return false;
        }
    }
}
