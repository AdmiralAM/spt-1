namespace SPTEconomy;

public sealed record AcquisitionCostDependency(string ItemTemplateId, double Count);

public sealed record AcquisitionCostPath
{
    public required string ItemTemplateId { get; init; }
    public required string PathId { get; init; }
    public required AcquisitionChannel Channel { get; init; }
    public double? FixedReferenceCost { get; init; }
    public required IReadOnlyList<AcquisitionCostDependency> Dependencies { get; init; }
    public int? EarliestProgressionLevel { get; init; }
    public double? ProductionTimeSeconds { get; init; }
}

public sealed record EffectiveAcquisitionReference
{
    public required string ItemTemplateId { get; init; }
    public required bool Known { get; init; }
    public double? Cost { get; init; }
    public string? SelectedPathId { get; init; }
    public required string State { get; init; }
    public required int PathsConsidered { get; init; }
}

public sealed record EffectiveAcquisitionGraphResult
{
    public required IReadOnlyList<EffectiveAcquisitionReference> Items { get; init; }
    public required int PathCount { get; init; }
    public required int ResolvedItemCount { get; init; }
    public required int UnknownItemCount { get; init; }
    public required int CycleBlockCount { get; init; }
    public required int DepthBlockCount { get; init; }
}

public static class EffectiveAcquisitionGraph
{
    public const int DefaultMaxDepth = 8;
    public const int DefaultMaxPathsPerItem = 32;

    public static EffectiveAcquisitionGraphResult Resolve(
        IEnumerable<AcquisitionCostPath> paths,
        int maxDepth = DefaultMaxDepth,
        int maxPathsPerItem = DefaultMaxPathsPerItem)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (maxDepth < 1 || maxDepth > 64) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        if (maxPathsPerItem < 1 || maxPathsPerItem > 1024) throw new ArgumentOutOfRangeException(nameof(maxPathsPerItem));

        var normalized = paths.Select(Validate).OrderBy(p => p.ItemTemplateId, StringComparer.Ordinal)
            .ThenBy(p => p.PathId, StringComparer.Ordinal).ToList();
        var duplicate = normalized.GroupBy(p => (p.ItemTemplateId, p.PathId), StringTupleComparer.Instance)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Economy Admiral acquisition graph: duplicate path identity '{duplicate.Key.ItemTemplateId}/{duplicate.Key.PathId}'.");

        var byItem = normalized.GroupBy(p => p.ItemTemplateId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Take(maxPathsPerItem).ToArray(), StringComparer.Ordinal);
        var memo = new Dictionary<string, EffectiveAcquisitionReference>(StringComparer.Ordinal);
        var cycleBlocks = 0;
        var depthBlocks = 0;

        EffectiveAcquisitionReference ResolveItem(string itemId, int depth, HashSet<string> visiting)
        {
            if (memo.TryGetValue(itemId, out var cached)) return cached;
            if (depth > maxDepth)
            {
                depthBlocks++;
                return new EffectiveAcquisitionReference { ItemTemplateId = itemId, Known = false, State = "DepthLimit", PathsConsidered = 0 };
            }
            if (!visiting.Add(itemId))
            {
                cycleBlocks++;
                return new EffectiveAcquisitionReference { ItemTemplateId = itemId, Known = false, State = "Cycle", PathsConsidered = 0 };
            }
            if (!byItem.TryGetValue(itemId, out var itemPaths) || itemPaths.Length == 0)
            {
                visiting.Remove(itemId);
                var unknown = new EffectiveAcquisitionReference { ItemTemplateId = itemId, Known = false, State = "NoEligiblePath", PathsConsidered = 0 };
                memo[itemId] = unknown;
                return unknown;
            }

            double? bestCost = null;
            string? bestPath = null;
            foreach (var path in itemPaths)
            {
                var cost = path.FixedReferenceCost ?? 0d;
                var known = path.FixedReferenceCost.HasValue || path.Dependencies.Count > 0;
                foreach (var dependency in path.Dependencies)
                {
                    var resolved = ResolveItem(dependency.ItemTemplateId, depth + 1, visiting);
                    if (!resolved.Known || !resolved.Cost.HasValue)
                    {
                        known = false;
                        break;
                    }
                    cost += resolved.Cost.Value * dependency.Count;
                    if (!double.IsFinite(cost))
                    {
                        known = false;
                        break;
                    }
                }
                if (!known || cost < 0) continue;
                if (!bestCost.HasValue || cost < bestCost.Value || (Math.Abs(cost - bestCost.Value) < 0.000001 && string.CompareOrdinal(path.PathId, bestPath) < 0))
                {
                    bestCost = cost;
                    bestPath = path.PathId;
                }
            }

            visiting.Remove(itemId);
            var result = bestCost.HasValue
                ? new EffectiveAcquisitionReference { ItemTemplateId = itemId, Known = true, Cost = Math.Round(bestCost.Value, 4), SelectedPathId = bestPath, State = "Resolved", PathsConsidered = itemPaths.Length }
                : new EffectiveAcquisitionReference { ItemTemplateId = itemId, Known = false, State = "UnknownDependencies", PathsConsidered = itemPaths.Length };
            memo[itemId] = result;
            return result;
        }

        var items = byItem.Keys.OrderBy(x => x, StringComparer.Ordinal)
            .Select(item => ResolveItem(item, 0, new HashSet<string>(StringComparer.Ordinal))).ToArray();
        return new EffectiveAcquisitionGraphResult
        {
            Items = items,
            PathCount = normalized.Count,
            ResolvedItemCount = items.Count(x => x.Known),
            UnknownItemCount = items.Count(x => !x.Known),
            CycleBlockCount = cycleBlocks,
            DepthBlockCount = depthBlocks,
        };
    }

    private static AcquisitionCostPath Validate(AcquisitionCostPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path.ItemTemplateId) || string.IsNullOrWhiteSpace(path.PathId))
            throw new InvalidOperationException("Economy Admiral acquisition graph: item/path identity must not be empty.");
        if (path.FixedReferenceCost.HasValue && (!double.IsFinite(path.FixedReferenceCost.Value) || path.FixedReferenceCost.Value < 0))
            throw new InvalidOperationException("Economy Admiral acquisition graph: fixed reference cost must be finite and non-negative.");
        foreach (var dependency in path.Dependencies)
        {
            if (string.IsNullOrWhiteSpace(dependency.ItemTemplateId) || !double.IsFinite(dependency.Count) || dependency.Count <= 0)
                throw new InvalidOperationException("Economy Admiral acquisition graph: dependency identity/count is invalid.");
        }
        return path with
        {
            ItemTemplateId = path.ItemTemplateId.Trim(),
            PathId = path.PathId.Trim(),
            Dependencies = path.Dependencies.OrderBy(d => d.ItemTemplateId, StringComparer.Ordinal).ThenBy(d => d.Count).ToArray(),
        };
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string ItemTemplateId, string PathId)>
    {
        public static readonly StringTupleComparer Instance = new();
        public bool Equals((string ItemTemplateId, string PathId) x, (string ItemTemplateId, string PathId) y) =>
            StringComparer.Ordinal.Equals(x.ItemTemplateId, y.ItemTemplateId) && StringComparer.Ordinal.Equals(x.PathId, y.PathId);
        public int GetHashCode((string ItemTemplateId, string PathId) obj) => HashCode.Combine(StringComparer.Ordinal.GetHashCode(obj.ItemTemplateId), StringComparer.Ordinal.GetHashCode(obj.PathId));
    }
}
