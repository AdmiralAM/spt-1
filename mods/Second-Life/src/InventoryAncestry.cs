namespace Admiral.SecondLife;

public static class InventoryAncestry
{
    public static bool IsBelow(
        string itemId,
        string rootId,
        IReadOnlyDictionary<string, string?> parentById,
        int maximumDepth = 32)
    {
        if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(rootId) || parentById is null || maximumDepth <= 0)
            return false;

        string current = itemId;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (int depth = 0; depth < maximumDepth && visited.Add(current); depth++)
        {
            if (!parentById.TryGetValue(current, out string? parent) || string.IsNullOrWhiteSpace(parent)) return false;
            if (string.Equals(parent, rootId, StringComparison.Ordinal)) return true;
            current = parent;
        }
        return false;
    }
}
