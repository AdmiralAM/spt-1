using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidFocusEvidenceTests
    {
        [Fact]
        public void Build_SeparatesExecutableFrontierMatchFromBlockedPathContext()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "target",
                new[] { "a1", "a", "b", "target" },
                new[] { "a1", "b" });
            PlannerRaidDecisionSignals signals = Signals(
                new[] { "a", "b" },
                new[] { "a", "target" });

            PlannerRaidFocusEvidence evidence = PlannerRaidFocusEvidenceBuilder.Build(signals, intent);

            Assert.Equal(new[] { "b" }, evidence.MatchedFrontierQuestIds);
            Assert.Equal(new[] { "a", "target" }, evidence.FocusedImmediateUnlockQuestIds);
            Assert.True(evidence.AdvancesExecutableFocus);
            Assert.True(evidence.HasFocusedImmediateLeverage);
        }

        [Fact]
        public void Build_DoesNotTreatUnrelatedUnlockAsFocusedLeverage()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "target",
                new[] { "a", "target" },
                new[] { "a" });
            PlannerRaidDecisionSignals signals = Signals(
                new[] { "a" },
                new[] { "side-quest" });

            PlannerRaidFocusEvidence evidence = PlannerRaidFocusEvidenceBuilder.Build(signals, intent);

            Assert.Equal(new[] { "a" }, evidence.MatchedFrontierQuestIds);
            Assert.Empty(evidence.FocusedImmediateUnlockQuestIds);
            Assert.True(evidence.AdvancesExecutableFocus);
            Assert.False(evidence.HasFocusedImmediateLeverage);
        }

        [Fact]
        public void Build_EmptyIntentProducesNoFocusEvidence()
        {
            PlannerRaidFocusEvidence evidence = PlannerRaidFocusEvidenceBuilder.Build(
                Signals(new[] { "a" }, new[] { "target" }),
                new PlannerRaidDecisionIntent());

            Assert.Empty(evidence.MatchedFrontierQuestIds);
            Assert.Empty(evidence.FocusedImmediateUnlockQuestIds);
            Assert.False(evidence.AdvancesExecutableFocus);
        }

        private static PlannerRaidDecisionSignals Signals(string[] questIds, string[] unlockIds)
        {
            return new PlannerRaidDecisionSignals(
                questIds.Length,
                0,
                Array.Empty<PlannerRaidActionOverlap>(),
                unlockIds.Length,
                0,
                0,
                0,
                questIds.Length,
                0,
                nonRepeatableQuestIds: questIds,
                immediateUnlockQuestIds: unlockIds);
        }
    }
}
