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
}
