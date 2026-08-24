using System;
using System.Collections.Generic;

namespace SPTQuestPlanner.Client
{
    public sealed class PlannerRecommendationSnapshot
    {
        public PlannerRecommendationSnapshot(
            long cacheRevision,
            long generatedAtUnixSeconds,
            int requestedTopN,
            IReadOnlyList<PlannerRecommendationViewModel> recommendations)
        {
            CacheRevision = cacheRevision;
            GeneratedAtUnixSeconds = Math.Max(0L, generatedAtUnixSeconds);
            RequestedTopN = Math.Max(1, requestedTopN);
            Recommendations = recommendations ?? Array.Empty<PlannerRecommendationViewModel>();
        }

        public long CacheRevision { get; private set; }
        public long GeneratedAtUnixSeconds { get; private set; }
        public int RequestedTopN { get; private set; }
        public IReadOnlyList<PlannerRecommendationViewModel> Recommendations { get; private set; }
    }

    public sealed class PlannerRecommendationProvider
    {
        private readonly PlannerClientCache cache;
        private readonly object gate = new object();
        private long cachedRevision = -1;
        private int cachedTopN;
        private string cachedPolicyKey;
        private PlannerRecommendationSnapshot cached;

        public PlannerRecommendationProvider(PlannerClientCache cache)
        {
            this.cache = cache ?? throw new ArgumentNullException("cache");
        }

        public PlannerRecommendationSnapshot Get(
            int topN = 5,
            PlannerCandidatePolicy policy = null)
        {
            if (topN < 1 || topN > 32)
                throw new ArgumentOutOfRangeException("topN", "Recommendation count must be between 1 and 32.");

            policy = policy ?? new PlannerCandidatePolicy();
            string policyKey = Key(policy);

            lock (gate)
            {
                if (!cache.HasTopology || !cache.HasState)
                {
                    long generated = cache.Index == null ? 0L : cache.Index.GeneratedAtUnixSeconds;
                    return new PlannerRecommendationSnapshot(cache.Revision, generated, topN, Array.Empty<PlannerRecommendationViewModel>());
                }

                long revision = cache.Revision;
                if (cached != null && cachedRevision == revision && cachedTopN == topN && string.Equals(cachedPolicyKey, policyKey, StringComparison.Ordinal))
                    return cached;

                PlannerTopologyIndex topology = cache.TopologyIndex;
                PlannerRequirementIndex requirements = cache.RequirementIndex;
                PlannerClientIndex state = cache.Index;
                PlannerQueryEngine query = new PlannerQueryEngine(topology, state);
                PlannerPathItemPlanner itemPlanner = new PlannerPathItemPlanner(query, requirements, state);
                PlannerCandidateSelector selector = new PlannerCandidateSelector(state);
                PlannerRoutePrioritizer prioritizer = new PlannerRoutePrioritizer(query, itemPlanner, state);
                PlannerRecommendationEngine engine = new PlannerRecommendationEngine(selector, prioritizer);
                IReadOnlyList<PlannerRecommendation> recommendations = engine.Recommend(topN, policy);
                PlannerRecommendationViewModelBuilder builder = new PlannerRecommendationViewModelBuilder(
                    topology,
                    cache.LocaleIndex,
                    query);
                IReadOnlyList<PlannerRecommendationViewModel> viewModels = builder.Build(recommendations);

                cachedRevision = revision;
                cachedTopN = topN;
                cachedPolicyKey = policyKey;
                cached = new PlannerRecommendationSnapshot(
                    revision,
                    state.GeneratedAtUnixSeconds,
                    topN,
                    Copy(viewModels));
                return cached;
            }
        }

        private static string Key(PlannerCandidatePolicy policy)
        {
            return (policy.IncludeActive ? "1" : "0") +
                   (policy.IncludeAvailable ? "1" : "0") +
                   (policy.IncludeReachable ? "1" : "0") +
                   (policy.IncludeBlocked ? "1" : "0");
        }

        private static PlannerRecommendationViewModel[] Copy(IReadOnlyList<PlannerRecommendationViewModel> values)
        {
            PlannerRecommendationViewModel[] result = new PlannerRecommendationViewModel[values.Count];
            for (int i = 0; i < values.Count; i++) result[i] = values[i];
            return result;
        }
    }
}
