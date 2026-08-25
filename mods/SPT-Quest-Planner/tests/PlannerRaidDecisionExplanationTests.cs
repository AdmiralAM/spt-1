using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidDecisionExplanationTests
    {
        [Fact]
        public void ExplanationCarriesConcreteQuestAndUnlockIds()
        {
            PlannerRaidDecisionSignals signals = new PlannerRaidDecisionSignals(
                2,
                1,
                new[]
                {
                    new PlannerRaidActionOverlap(
                        "kill|customs|pmc",
                        PlannerRaidObjectiveKind.Kill,
                        new[] { "Setup", "ShooterBorn" },
                        2)
                },
                2,
                0,
                0,
                1,
                3,
                3,
                new[] { "Setup", "ShooterBorn" },
                new[] { "DailyKill" },
                new[] { "SilentCaliber", "InformedMeansArmed" });

            PlannerRaidDecisionExplanation explanation = PlannerRaidDecisionExplanationBuilder.Build("customs", signals);

            Assert.Equal(new[] { "Setup", "ShooterBorn" }, explanation.ProgressionQuestIds);
            Assert.Equal(new[] { "SilentCaliber", "InformedMeansArmed" }, explanation.ImmediateUnlockQuestIds);
            Assert.True(explanation.HasCrossQuestSynergy);
            Assert.True(explanation.HasProgressionLeverage);
            Assert.Contains(explanation.Cautions, value => value.Contains("repeatable", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(explanation.Cautions, value => value.Contains("unresolved semantics", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ExplanationKeepsPreparationAsExplicitState()
        {
            PlannerRaidDecisionSignals signals = new PlannerRaidDecisionSignals(
                1,
                0,
                Array.Empty<PlannerRaidActionOverlap>(),
                0,
                2,
                1,
                0,
                1,
                1,
                new[] { "Quest" },
                Array.Empty<string>(),
                Array.Empty<string>());

            PlannerRaidDecisionExplanation explanation = PlannerRaidDecisionExplanationBuilder.Build("reserve", signals);

            Assert.False(explanation.PreparationReady);
            Assert.Equal(2, explanation.MissingPreparationTemplateCount);
            Assert.Equal(1, explanation.UnresolvedPreparationCount);
            Assert.Contains(explanation.Cautions, value => value.Contains("Preparation", StringComparison.OrdinalIgnoreCase));
        }
    }
}
