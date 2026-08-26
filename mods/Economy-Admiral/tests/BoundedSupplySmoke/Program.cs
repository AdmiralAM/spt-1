using SPTEconomy;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void MustFail(string name, Action action)
{
    try { action(); }
    catch (InvalidOperationException) { Console.WriteLine($"PASS {name}"); return; }
    throw new InvalidOperationException($"Expected '{name}' to fail.");
}

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
Require(result.Count == 3, "Only items with renewable sources should receive bounded-supply summaries.");

var admiral = result.Single(item => item.ItemTemplateId == "ammo-admiral");
Require(admiral.RenewableSourceCount == 1, "Admiral source count mismatch.");
Require(admiral.KnownBoundedRenewableSourceCount == 1, "Admiral source must be classified bounded.");
Require(admiral.KnownUnboundedRenewableSourceCount == 0, "Admiral source must not appear unbounded.");
Require(admiral.UnknownCapacityRenewableSourceCount == 0, "Admiral capacity evidence should be complete.");
Require(admiral.CapacityEvidenceCoverage == 1.0 && admiral.HasCompleteCapacityEvidence, "Admiral capacity evidence completeness mismatch.");
Require(admiral.HasOnlyKnownBoundedRenewablePaths, "Quest-gated finite Admiral offer should remain a bounded renewable path.");
Require(!admiral.HasKnownUnboundedRenewablePath, "Finite Admiral offer must not be treated as unbounded supply.");
Require(admiral.TotalKnownMaxUnitsPerReset == 400, "Admiral finite unit cap mismatch.");
Require(admiral.TotalKnownMaxAcquisitionsPerReset == 4, "Admiral finite acquisition cap mismatch.");

var unlimited = result.Single(item => item.ItemTemplateId == "ammo-unlimited");
Require(unlimited.HasKnownUnboundedRenewablePath, "Unlimited source must remain distinguishable from bounded supply.");
Require(!unlimited.HasOnlyKnownBoundedRenewablePaths, "Unlimited source cannot be bounded-only.");
Require(unlimited.TotalKnownMaxUnitsPerReset is null, "Unbounded source must not fabricate a finite unit cap.");

var mixed = result.Single(item => item.ItemTemplateId == "ammo-mixed");
Require(mixed.RenewableSourceCount == 2, "Mixed source count mismatch.");
Require(mixed.KnownBoundedRenewableSourceCount == 1, "Mixed bounded source count mismatch.");
Require(mixed.UnknownCapacityRenewableSourceCount == 1, "Missing craft capacity must remain unknown.");
Require(mixed.CapacityEvidenceCoverage == 0.5 && !mixed.HasCompleteCapacityEvidence, "Partial capacity evidence must remain incomplete.");
Require(!mixed.HasOnlyKnownBoundedRenewablePaths, "Unknown capacity prevents claiming bounded-only supply.");
Require(!mixed.HasKnownUnboundedRenewablePath, "Unknown capacity must not be promoted to unbounded.");

Require(result.All(item => item.ItemTemplateId != "sample-only"), "One-time sample rewards must not be represented as renewable supply capacity.");

var reversed = BoundedSupplyEvidenceAnalyzer.Analyze(sources.Reverse(), capacities.Reverse());
Require(
    result.Select(item => item.ItemTemplateId).SequenceEqual(reversed.Select(item => item.ItemTemplateId)),
    "Bounded supply output ordering must be deterministic."
);
Require(
    result.Select(item => item.CapacityEvidenceCoverage).SequenceEqual(reversed.Select(item => item.CapacityEvidenceCoverage)),
    "Bounded supply metrics must be input-order independent."
);

MustFail("bounded without limit", () => BoundedSupplyEvidenceAnalyzer.Analyze(
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

MustFail("unbounded with finite limit", () => BoundedSupplyEvidenceAnalyzer.Analyze(
    sources,
    new[]
    {
        new RenewableSupplyCapacityEvidence
        {
            ItemTemplateId = "ammo-unlimited",
            SourceId = "generic:unlimited-offer",
            Channel = AcquisitionChannel.TraderPurchase,
            SupplyBound = RenewableSupplyBound.Unbounded,
            MaxUnitsPerReset = 100,
        },
    }
));

MustFail("capacity for one-time source", () => BoundedSupplyEvidenceAnalyzer.Analyze(
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

MustFail("conflicting capacity duplicate", () => BoundedSupplyEvidenceAnalyzer.Analyze(
    sources,
    new[]
    {
        new RenewableSupplyCapacityEvidence
        {
            ItemTemplateId = "ammo-admiral",
            SourceId = "admiral:quest-gated-offer",
            Channel = AcquisitionChannel.TraderPurchase,
            SupplyBound = RenewableSupplyBound.Bounded,
            MaxUnitsPerReset = 400,
        },
        new RenewableSupplyCapacityEvidence
        {
            ItemTemplateId = "ammo-admiral",
            SourceId = "admiral:quest-gated-offer",
            Channel = AcquisitionChannel.TraderPurchase,
            SupplyBound = RenewableSupplyBound.Bounded,
            MaxUnitsPerReset = 500,
        },
    }
));

Console.WriteLine("Economy Admiral bounded supply smoke PASS");
