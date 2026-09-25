namespace AdmiralCompatibilitySuite.Seasons;

internal sealed record SeasonRotationState(int Version, int Season, int CompletedInSeason, string? LastServerId);

internal static class SeasonCyclePolicy
{
    public const int RaidsPerSeason = 5;
    public static readonly int[] UpstreamOrder = [0, 1, 4, 6, 2, 5, 3];

    public static SeasonRotationState? Advance(SeasonRotationState current, IReadOnlyList<int> enabledSeasons, string? serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId) || serverId == current.LastServerId)
            return null;
        int position = -1;
        for (int i = 0; i < enabledSeasons.Count; i++)
            if (enabledSeasons[i] == current.Season) position = i;
        if (position < 0 || enabledSeasons.Count < 2)
            throw new InvalidOperationException("Current season is not in the enabled upstream cycle.");

        int completed = current.CompletedInSeason + 1;
        int season = current.Season;
        if (completed == RaidsPerSeason)
        {
            completed = 0;
            season = enabledSeasons[(position + 1) % enabledSeasons.Count];
        }
        return new SeasonRotationState(1, season, completed, serverId);
    }
}
