using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests;

public sealed class PlannerRaidPlanProviderTests
{
    [Fact]
    public void ReturnsEmptyCollectionUntilTopologyAndStateAreReady()
    {
        PlannerClientCache cache = new();
        PlannerRaidPlanProvider provider = new(cache);

        PlannerRaidPlanCollection result = provider.Get();

        Assert.Empty(result.Plans);
        Assert.Equal(0, result.GeneratedAtUnixSeconds);
    }

    [Fact]
    public void ReusesDerivedCollectionWithinSameCacheRevisionAndInvalidatesAfterStateSwap()
    {
        PlannerClientCache cache = new();
        PlannerLocationObjective objective = new(
            "q1", "visit", "VisitPlace", "Finish", null,
            Array.Empty<string>(), new[] { "Customs" }, PlannerObjectiveKind.Visit);
        PlannerLocationIndex locations = new(
            new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customs"] = new PlannerLocationBucket("Customs", new[] { objective })
            },
            Array.Empty<PlannerLocationObjective>());

        cache.ReplaceTopology(
            new PlannerPayload(9, 0, "topology"),
            new PlannerTopologyIndex(
                new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal),
                new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal)),
            new PlannerRequirementIndex(
                new Dictionary<string, IReadOnlyList<PlannerQuestItemRequirement>>(StringComparer.Ordinal)),
            locations);
        cache.ReplaceState(new PlannerPayload(9, 10, "state-10"), State(10));

        PlannerRaidPlanProvider provider = new(cache);
        PlannerRaidPlanCollection first = provider.Get();
        PlannerRaidPlanCollection second = provider.Get();

        Assert.Same(first, second);
        Assert.Equal(10, first.GeneratedAtUnixSeconds);
        Assert.Single(first.Plans);

        cache.ReplaceState(new PlannerPayload(9, 20, "state-20"), State(20));
        PlannerRaidPlanCollection refreshed = provider.Get();

        Assert.NotSame(first, refreshed);
        Assert.Equal(20, refreshed.GeneratedAtUnixSeconds);
        Assert.Single(refreshed.Plans);
    }

    private static PlannerClientIndex State(long generated)
    {
        return new PlannerClientIndex(
            generated,
            new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q1"] = new PlannerQuestClientState("q1", 4, 2, true, true)
            },
            new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
    }
}
