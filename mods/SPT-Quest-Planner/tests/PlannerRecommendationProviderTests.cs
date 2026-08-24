using System;
using System.Collections.Generic;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRecommendationProviderTests
    {
        [Fact]
        public void Get_ReusesSnapshotForSameRevisionAndPolicy()
        {
            PlannerClientCache cache = BuildCache(100, 4);
            PlannerRecommendationProvider provider = new PlannerRecommendationProvider(cache);

            PlannerRecommendationSnapshot first = provider.Get(5);
            PlannerRecommendationSnapshot second = provider.Get(5);

            Assert.Same(first, second);
            Assert.Equal(cache.Revision, first.CacheRevision);
            Assert.Single(first.Recommendations);
        }

        [Fact]
        public void Get_RebuildsAfterStateRevisionChanges()
        {
            PlannerClientCache cache = BuildCache(100, 4);
            PlannerRecommendationProvider provider = new PlannerRecommendationProvider(cache);
            PlannerRecommendationSnapshot first = provider.Get(5);

            ReplaceState(cache, 200, 3);
            PlannerRecommendationSnapshot second = provider.Get(5);

            Assert.NotSame(first, second);
            Assert.True(second.CacheRevision > first.CacheRevision);
            Assert.Equal(200, second.GeneratedAtUnixSeconds);
            Assert.Equal(3, second.Recommendations[0].Disposition);
        }

        [Fact]
        public void Get_UsesDifferentCacheEntryWhenPolicyChanges()
        {
            PlannerClientCache cache = BuildCache(100, 1);
            PlannerRecommendationProvider provider = new PlannerRecommendationProvider(cache);

            PlannerRecommendationSnapshot defaultResult = provider.Get(5);
            PlannerRecommendationSnapshot includeBlocked = provider.Get(5, new PlannerCandidatePolicy(includeBlocked: true));

            Assert.Empty(defaultResult.Recommendations);
            Assert.Single(includeBlocked.Recommendations);
            Assert.NotSame(defaultResult, includeBlocked);
        }

        private static PlannerClientCache BuildCache(long generated, int disposition)
        {
            PlannerClientCache cache = new PlannerClientCache();
            Dictionary<string, PlannerTopologyQuest> quests = new Dictionary<string, PlannerTopologyQuest>(StringComparer.Ordinal)
            {
                ["q"] = new PlannerTopologyQuest("q", "trader", "Quest Q", null, false, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
            };
            cache.ReplaceTopology(
                new PlannerPayload(PlannerClientContract.SchemaVersion, 0, "{}"),
                new PlannerTopologyIndex(quests, new Dictionary<string, PlannerTopologyItem>(StringComparer.Ordinal)),
                new PlannerRequirementIndex(new Dictionary<string, IReadOnlyList<PlannerQuestItemRequirement>>(StringComparer.Ordinal)),
                new PlannerLocationIndex(new Dictionary<string, PlannerLocationBucket>(StringComparer.OrdinalIgnoreCase), Array.Empty<PlannerLocationObjective>()));
            ReplaceState(cache, generated, disposition);
            return cache;
        }

        private static void ReplaceState(PlannerClientCache cache, long generated, int disposition)
        {
            Dictionary<string, PlannerQuestClientState> quests = new Dictionary<string, PlannerQuestClientState>(StringComparer.Ordinal)
            {
                ["q"] = new PlannerQuestClientState("q", disposition, 0, true, true)
            };
            PlannerClientIndex state = new PlannerClientIndex(generated, quests, new Dictionary<string, PlannerItemClientState>(StringComparer.Ordinal));
            cache.ReplaceState(new PlannerPayload(PlannerClientContract.SchemaVersion, generated, "{}"), state);
        }
    }
}
