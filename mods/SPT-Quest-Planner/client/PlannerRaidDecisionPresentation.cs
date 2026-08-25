using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTQuestPlanner.Client
{
    public enum PlannerRaidDecisionPresentationKind
    {
        NoRecommendation = 0,
        BestNextRaid = 1,
        SeveralGoodOptions = 2
    }

    public sealed class PlannerRaidDecisionPresentation
    {
        public PlannerRaidDecisionPresentation(
            PlannerRaidDecisionPresentationKind kind,
            PlannerRaidDecisionExplanation primary,
            IReadOnlyList<PlannerRaidDecisionExplanation> alternatives,
            string headline,
            string summary)
        {
            Kind = kind;
            Primary = primary;
            Alternatives = alternatives ?? Array.Empty<PlannerRaidDecisionExplanation>();
            Headline = headline ?? string.Empty;
            Summary = summary ?? string.Empty;
        }

        public PlannerRaidDecisionPresentationKind Kind { get; private set; }
        public PlannerRaidDecisionExplanation Primary { get; private set; }
        public IReadOnlyList<PlannerRaidDecisionExplanation> Alternatives { get; private set; }
        public string Headline { get; private set; }
        public string Summary { get; private set; }
    }

    public static class PlannerRaidDecisionPresentationBuilder
    {
        public static PlannerRaidDecisionPresentation Build(PlannerRaidDecisionSet set)
        {
            if (set == null) throw new ArgumentNullException("set");

            if (set.HasUniqueRecommendation && set.Recommendation != null)
            {
                PlannerRaidDecisionExplanation primary = PlannerRaidDecisionExplanationBuilder.Build(
                    set.Recommendation.LocationId,
                    set.Recommendation.Signals);

                return new PlannerRaidDecisionPresentation(
                    PlannerRaidDecisionPresentationKind.BestNextRaid,
                    primary,
                    Array.Empty<PlannerRaidDecisionExplanation>(),
                    "Best next raid",
                    set.Reason);
            }

            PlannerRaidDecisionExplanation[] contenders = set.Contenders
                .Select(value => PlannerRaidDecisionExplanationBuilder.Build(value.LocationId, value.Signals))
                .OrderBy(value => value.LocationId, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (contenders.Length > 1)
            {
                return new PlannerRaidDecisionPresentation(
                    PlannerRaidDecisionPresentationKind.SeveralGoodOptions,
                    null,
                    contenders,
                    "Several good options",
                    set.Reason);
            }

            return new PlannerRaidDecisionPresentation(
                PlannerRaidDecisionPresentationKind.NoRecommendation,
                null,
                contenders,
                "No meaningful recommendation",
                string.IsNullOrWhiteSpace(set.Reason)
                    ? "No proven decision advantage is currently available."
                    : set.Reason);
        }
    }
}
