using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class BoundedSupplyAssertions
{
    [ModuleInitializer]
    internal static void Run()
    {
        var sources = new[]
        {
            new AcquisitionSourceEvidence
            {
                ItemTemplateId = "ammo-admiral",
                SourceId = "admiral:quest-gated-offer",
                Channel = AcquisitionChannel.TraderPurchase,
                Renewable = true,
                EarliestProgressionLevel = 20,
                ProvenanceClass = "ModAdded",
            },
            new AcquisitionSourceEvidence
            {
                ItemTemplateId = "ammo-unlimited",
                SourceId = "generic:unlimited-offer",
                Channel = AcquisitionChannel.TraderPurchase,
                Renewable = true,
                EarliestProgressionLevel = 1,
                ProvenanceClass = "ModAdded",
            },
            new AcquisitionSourceEvidence
            {
                ItemTemplateId = "ammo-mixed",
                SourceId = "bounded-offer",
                Channel = AcquisitionChannel.TraderPurchase,
                Renewable = true,
                EarliestProgressionLevel = 10,
                ProvenanceClass = "ModAdded",
            },
            new AcquisitionSourceEvidence
            {
                ItemTemplateId = "ammo-mixed",
                SourceId = "unknown-craft",
                Channel = AcquisitionChannel.Craft,
                Renewable = true,
                EarliestProgressionLevel = 12,
                ProvenanceClass = "ModAdded",
            },
            new AcquisitionSourceEvidence
            {
                ItemTemplateId = "sample-only",
                SourceId = "one-time-quest-sample",
                Channel = AcquisitionChannel.QuestReward,
                Renewable = false,
                EarliestProgressionLevel = 30,
                ProvenanceClass = "ModAdded",
            },
        };

        var capacities = new[]
        {
            new RenewableSupplyCapacityEvidence
            {
                ItemTemplateId = "ammo-admiral",
                SourceId = "admiral:quest-gated-offer",
                Channel = AcquisitionChannel.TraderPurchase,
                SupplyBound = RenewableSupplyBound.Bounded,
                MaxUnitsPerReset = 400,
                MaxAcquisitionsPerReset = 4,
            },
            new RenewableSupplyCapacityEvidence
            {
                ItemTemplateId = "ammo-unlimited",
                SourceId = "generic:unlimited-offer",
                Channel = AcquisitionChannel.TraderPurchase,
                SupplyBound = RenewableSupplyBound.Unbounded,
            },
            new RenewableSupplyCapacityEvidence
            {
                ItemTemplateId = "ammo-mixed",
                SourceId = "bounded-offer",
                Channel = AcquisitionChannel.TraderPurchase,
                SupplyBound = RenewableSupplyBound.Bounded,
                MaxUnitsPerReset = 100,
                MaxAcquisitionsPerReset = 1,
            },
        };

        var result = BoundedSupplyEvidenceAnalyzer.Analyze(sources, capacities);
        Require(result.Count == 3, "Only renewable items should receive capacity summaries.");

        var admiral = result.Single(item => item.ItemTemplateId == "ammo-admiral");
        Require(admiral.KnownBoundedRenewableSourceCount == 1, "Admiral finite offer must be bounded.");
        Require(admiral.HasOnlyKnownBoundedRenewablePaths, "Admiral finite offer must remain bounded-only.");
        Require(!admiral.HasKnownUnboundedRenewablePath, "Admiral finite offer must not be promoted to unbounded supply.");
        Require(admiral.TotalKnownMaxUnitsPerReset == 400, "Admiral unit cap mismatch.");
        Require(admiral.TotalKnownMaxAcquisitionsPerReset == 4, "Admiral acquisition cap mismatch.");

        var unlimited = result.Single(item => item.ItemTemplateId == "ammo-unlimited");
        Require(unlimited.HasKnownUnboundedRenewablePath, "Unlimited source must remain distinguishable.");
        Require(!unlimited.HasOnlyKnownBoundedRenewablePaths, "Unlimited source cannot be bounded-only.");

        var mixed = result.Single(item => item.ItemTemplateId == "ammo-mixed");
        Require(mixed.KnownBoundedRenewableSourceCount == 1, "Mixed bounded count mismatch.");
        Require(mixed.UnknownCapacityRenewableSourceCount == 1, "Missing craft capacity must remain unknown.");
        Require(mixed.CapacityEvidenceCoverage == 0.5 && !mixed.HasCompleteCapacityEvidence, "Partial capacity evidence must remain incomplete.");
        Require(!mixed.HasOnlyKnownBoundedRenewablePaths, "Unknown capacity prevents bounded-only claim.");
        Require(!mixed.HasKnownUnboundedRenewablePath, "Unknown capacity must not become unbounded.");

        Require(result.All(item => item.ItemTemplateId != "sample-only"), "One-time sample reward must not become renewable supply.");

        var reversed = BoundedSupplyEvidenceAnalyzer.Analyze(sources.Reverse(), capacities.Reverse());
        Require(
            result.Select(item => item.ItemTemplateId).SequenceEqual(reversed.Select(item => item.ItemTemplateId)),
            "Bounded supply ordering must be deterministic."
        );

        MustFail(() => BoundedSupplyEvidenceAnalyzer.Analyze(
            sources,
            new[]
            {
                new RenewableSupplyCapacityEvidence
                {
                    ItemTemplateId = "ammo-admiral",
                    SourceId = "admiral:quest-gated-offer",
                    Channel = AcquisitionChannel.TraderPurchase,
                    SupplyBound = RenewableSupplyBound.Bounded,
                },
            }
        ));

        MustFail(() => BoundedSupplyEvidenceAnalyzer.Analyze(
            sources,
            new[]
            {
                new RenewableSupplyCapacityEvidence
                {
                    ItemTemplateId = "sample-only",
                    SourceId = "one-time-quest-sample",
                    Channel = AcquisitionChannel.QuestReward,
                    SupplyBound = RenewableSupplyBound.Bounded,
                    MaxUnitsPerReset = 1,
                },
            }
        ));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void MustFail(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected bounded-supply fixture to fail.");
    }
}
