using System;
using System.Linq;
using SPTQuestPlanner.Client;
using Xunit;

namespace SPTQuestPlanner.Tests
{
    public sealed class PlannerAdmiralCapabilityContractAdapterTests
    {
        [Fact]
        public void ParsesBoundedCapabilityAndOneTimeSampleFromPublishedContractShape()
        {
            var goals = PlannerAdmiralCapabilityContractAdapter.Parse(ContractJson());

            Assert.Equal(2, goals.Count);

            PlannerCapabilityGoalDefinition labs = goals.Single(value => value.CapabilityId == "labs-access");
            Assert.Equal("gate-labs", labs.GateQuestId);
            Assert.Equal("Admiral Trader", labs.Owner);
            Assert.Equal(PlannerCapabilitySupplyKind.BoundedRenewable, labs.SupplyKind);
            Assert.Equal("labs-card", labs.ItemTemplateId);
            Assert.Equal(1, labs.MaxUnitsPerReset);
            Assert.Equal(1, labs.MaxAcquisitionsPerReset);
            Assert.True(labs.HasBoundedSupplyEvidence);

            PlannerCapabilityGoalDefinition special = goals.Single(value => value.CapabilityId == "special-weapons");
            Assert.Equal("quest-special", special.GateQuestId);
            Assert.Equal(PlannerCapabilitySupplyKind.OneTimeSample, special.SupplyKind);
            Assert.Equal("rsp30", special.ItemTemplateId);
            Assert.False(special.HasBoundedSupplyEvidence);
            Assert.Null(special.MaxUnitsPerReset);
            Assert.Null(special.MaxAcquisitionsPerReset);
        }

        [Fact]
        public void ContractDriftFailsClosed()
        {
            string drifted = ContractJson().Replace("\"schemaVersion\":2", "\"schemaVersion\":3");
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => PlannerAdmiralCapabilityContractAdapter.Parse(drifted));

            Assert.Contains("schemaVersion", error.Message);
        }

        [Fact]
        public void UnlimitedOrUnboundedPermanentOfferCannotMasqueradeAsPublishedCapabilityEvidence()
        {
            string drifted = ContractJson().Replace("\"renewability\":\"Bounded\"", "\"renewability\":\"Unbounded\"");
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => PlannerAdmiralCapabilityContractAdapter.Parse(drifted));

            Assert.Contains("renewability", error.Message);
        }

        [Fact]
        public void DuplicateCapabilityFamilyFailsInsteadOfArbitrarilySelectingSource()
        {
            string duplicate = ContractJson().Replace(
                "\"capabilityFamily\":\"special-weapons\"",
                "\"capabilityFamily\":\"labs-access\"");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => PlannerAdmiralCapabilityContractAdapter.Parse(duplicate));

            Assert.Contains("duplicate capability", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        private static string ContractJson()
        {
            return "{" +
                "\"schemaVersion\":2," +
                "\"product\":\"Admiral Trader\"," +
                "\"owner\":\"Admiral Trader\"," +
                "\"targetSptVersion\":\"4.1.3\"," +
                "\"renewableOffers\":[{" +
                    "\"offerId\":\"offer-labs\"," +
                    "\"itemTpl\":\"labs-card\"," +
                    "\"capabilityFamily\":\"labs-access\"," +
                    "\"sourceType\":\"TraderPurchase\"," +
                    "\"renewability\":\"Bounded\"," +
                    "\"permanent\":true," +
                    "\"questGateId\":\"gate-labs\"," +
                    "\"stockPerReset\":1," +
                    "\"buyRestrictionPerReset\":1" +
                "}]," +
                "\"oneTimeRewards\":[{" +
                    "\"itemTpl\":\"rsp30\"," +
                    "\"capabilityFamily\":\"special-weapons\"," +
                    "\"sourceType\":\"QuestReward\"," +
                    "\"renewability\":\"OneTime\"," +
                    "\"permanent\":false," +
                    "\"sampleOnly\":true," +
                    "\"questId\":\"quest-special\"," +
                    "\"units\":1" +
                "}]" +
            "}";
        }
    }
}
