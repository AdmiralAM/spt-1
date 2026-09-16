namespace Admiral.SecondLife;

public readonly record struct WorldPoint(float X, float Y, float Z)
{
    public float SquaredDistanceTo(WorldPoint other)
    {
        float x = X - other.X;
        float y = Y - other.Y;
        float z = Z - other.Z;
        return (x * x) + (y * y) + (z * z);
    }
}

public readonly record struct SpawnCandidate(
    string Id,
    WorldPoint Position,
    bool NativeEligible,
    bool IsInvalidExtract);

public readonly record struct SafeSpawnPolicy(
    float MinimumKillerDistance,
    float MinimumCorpseDistance,
    float MinimumCombatDistance);

public static class SafeSpawnSelector
{
    public static SafeSpawnPolicy AdaptPolicyToMap(
        IEnumerable<SpawnCandidate> candidates,
        SafeSpawnPolicy configured)
    {
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));

        SpawnCandidate[] usable = candidates
            .Where(candidate => candidate.NativeEligible && !candidate.IsInvalidExtract)
            .ToArray();
        if (usable.Length < 2) return configured;

        float minX = usable.Min(candidate => candidate.Position.X);
        float maxX = usable.Max(candidate => candidate.Position.X);
        float minZ = usable.Min(candidate => candidate.Position.Z);
        float maxZ = usable.Max(candidate => candidate.Position.Z);
        float width = maxX - minX;
        float depth = maxZ - minZ;
        float mapSpan = MathF.Sqrt((width * width) + (depth * depth));

        return new SafeSpawnPolicy(
            Scale(configured.MinimumKillerDistance, mapSpan, 0.12f, 15f),
            Scale(configured.MinimumCorpseDistance, mapSpan, 0.18f, 20f),
            Scale(configured.MinimumCombatDistance, mapSpan, 0.06f, 10f));
    }

    public static bool TrySelect(
        IEnumerable<SpawnCandidate> candidates,
        string originalSpawnId,
        WorldPoint corpsePosition,
        WorldPoint? killerPosition,
        IEnumerable<WorldPoint> activeCombatPositions,
        SafeSpawnPolicy policy,
        int seed,
        out SpawnCandidate selected)
        => TrySelect(candidates, originalSpawnId, corpsePosition, killerPosition, activeCombatPositions, policy, seed, out selected, out _);

    public static bool TrySelect(
        IEnumerable<SpawnCandidate> candidates,
        string originalSpawnId,
        WorldPoint corpsePosition,
        WorldPoint? killerPosition,
        IEnumerable<WorldPoint> activeCombatPositions,
        SafeSpawnPolicy policy,
        int seed,
        out SpawnCandidate selected,
        out int eligibleCount)
    {
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));
        if (activeCombatPositions is null) throw new ArgumentNullException(nameof(activeCombatPositions));

        float killerDistance = Square(policy.MinimumKillerDistance);
        float corpseDistance = Square(policy.MinimumCorpseDistance);
        float combatDistance = Square(policy.MinimumCombatDistance);
        WorldPoint[] combat = activeCombatPositions.ToArray();

        SpawnCandidate[] eligible = candidates
            .Where(candidate => candidate.NativeEligible)
            .Where(candidate => !candidate.IsInvalidExtract)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Id))
            .Where(candidate => !string.Equals(candidate.Id, originalSpawnId, StringComparison.Ordinal))
            .Where(candidate => candidate.Position.SquaredDistanceTo(corpsePosition) >= corpseDistance)
            .Where(candidate => killerPosition is null || candidate.Position.SquaredDistanceTo(killerPosition.Value) >= killerDistance)
            .Where(candidate => combat.All(point => candidate.Position.SquaredDistanceTo(point) >= combatDistance))
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();

        eligibleCount = eligible.Length;

        if (eligible.Length == 0)
        {
            selected = default;
            return false;
        }

        string[] keys = eligible.Select(candidate => candidate.Id).ToArray();
        selected = eligible[DeterministicSelection.Index(seed, keys)];
        return true;
    }

    private static float Square(float value)
    {
        float bounded = Math.Max(0f, value);
        return bounded * bounded;
    }

    private static float Scale(float configured, float mapSpan, float fraction, float floor)
    {
        float boundedConfigured = Math.Max(0f, configured);
        if (boundedConfigured == 0f) return 0f;
        return Math.Min(boundedConfigured, Math.Max(floor, mapSpan * fraction));
    }
}
