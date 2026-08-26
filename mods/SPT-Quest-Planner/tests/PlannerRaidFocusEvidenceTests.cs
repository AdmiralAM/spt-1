using System;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerRaidFocusEvidenceTests
    {
        [Fact]
        public void Build_SeparatesActionableMatchFromBlockedPathContext()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "target",
                new[] { "a1", "a", "b", "target" },
                new[] { "a1", "b" },
                new[] { "b" },
                new[] { "a1" });
            PlannerRaidDecisionSignals signals = Signals(
                new[] { "a", "b" },
                new[] { "a", "target" });

            PlannerRaidFocusEvidence evidence = PlannerRaidFocusEvidenceBuilder.Build(signals, intent);

            Assert.Equal(new[] { "b" }, evidence.MatchedActionableQuestIds);
            Assert.Equal(new[] { "a", "target" }, evidence.FocusedImmediateUnlockQuestIds);
            Assert.Equal(new[] { "a1" }, evidence.EligibilityUnknownQuestIds);
            Assert.True(evidence.AdvancesActionableFocus);
            Assert.True(evidence.HasFocusedImmediateLeverage);
            Assert.True(evidence.HasEligibilityUnknowns);
        }

        [Fact]
        public void Build_DoesNotTreatPrerequisiteReadyButUnconfirmedQuestAsActionable()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "target",
                new[] { "a", "target" },
                new[] { "a" },
                Array.Empty<string>(),
                new[] { "a" });
            PlannerRaidDecisionSignals signals = Signals(new[] { "a" }, Array.Empty<string>());

            PlannerRaidFocusEvidence evidence = PlannerRaidFocusEvidenceBuilder.Build(signals, intent);

            Assert.Empty(evidence.MatchedActionableQuestIds);
            Assert.Equal(new[] { "a" }, evidence.EligibilityUnknownQuestIds);
            Assert.False(evidence.AdvancesActionableFocus);
            Assert.True(evidence.HasEligibilityUnknowns);
        }

        [Fact]
        public void Build_DoesNotTreatUnrelatedUnlockAsFocusedLeverage()
        {
            PlannerRaidDecisionIntent intent = new PlannerRaidDecisionIntent(
                "target",
                new[] { "a", "target" },
                new[] { "a" },
                new[] { "a" });
            PlannerRaidDecisionSignals signals = Signals(
                new[] { "a" },
                new[] { "side-quest" });

            PlannerRaidFocusEvidence evidence = PlannerRaidFocusEvidenceBuilder.Build(signals, intent);

            Assert.Equal(new[] { "a" }, evidence.MatchedActionableQuestIds);
            Assert.Empty(evidence.FocusedImmediateUnlockQuestIds);
            Assert.True(evidence.AdvancesActionableFocus);
            Assert.False(evidence.HasFocusedImmediateLeverage);
        }

        [Fact]
        public void Build_EmptyIntentProducesNoFocusEvidence()
        {
            PlannerRaidFocusEvidence evidence = PlannerRaidFocusEvidenceBuilder.Build(
                Signals(new[] { "a" }, new[] { "target" }),
                new PlannerRaidDecisionIntent());

            Assert.Empty(evidence.MatchedActionableQuestIds);
            Assert.Empty(evidence.FocusedImmediateUnlockQuestIds);
            Assert.False(evidence.AdvancesActionableFocus);
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
