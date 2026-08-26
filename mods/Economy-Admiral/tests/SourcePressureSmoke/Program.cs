using SPTEconomy;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void MustFail(string name, Action action)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine($"PASS {name}");
        return;
    }

    throw new InvalidOperationException($"Expected '{name}' to fail.");
}

var input = new[]
{
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item-a",
        SourceId = "trader-a:offer-1",
        Channel = AcquisitionChannel.TraderPurchase,
        Renewable = true,
        EarliestProgressionLevel = 1,
        ProvenanceClass = "PristineUnchanged",
    },
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item-a",
        SourceId = "quest-a",
        Channel = AcquisitionChannel.QuestReward,
        Renewable = false,
        EarliestProgressionLevel = 5,
        ProvenanceClass = "ModAdded",
    },
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item-a",
        SourceId = "craft-a",
        Channel = AcquisitionChannel.Craft,
        Renewable = true,
        EarliestProgressionLevel = 10,
        ProvenanceClass = "ModAdded",
    },
    // Exact duplicate edge must not inflate pressure evidence.
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item-a",
        SourceId = "craft-a",
        Channel = AcquisitionChannel.Craft,
        Renewable = true,
        EarliestProgressionLevel = 10,
        ProvenanceClass = "ModAdded",
    },
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item-b",
        SourceId = "quest-only",
        Channel = AcquisitionChannel.QuestReward,
        Renewable = false,
        EarliestProgressionLevel = null,
        ProvenanceClass = "PristineModified",
    },
};

var result = SourcePressureEvidenceAnalyzer.Analyze(input);
Require(result.Count == 2, "Expected two item summaries.");

var a = result.Single(item => item.ItemTemplateId == "item-a");
Require(a.SourceCount == 3, "item-a source count should deduplicate identical edges.");
Require(a.ChannelCount == 3, "item-a should expose three independent channels.");
Require(a.RenewableSourceCount == 2, "item-a renewable count mismatch.");
Require(a.OneTimeSourceCount == 1, "item-a one-time count mismatch.");
Require(a.RenewableSourceShare == 0.666667, "item-a renewable share mismatch.");
Require(a.HasRenewablePath, "item-a should retain renewable acquisition.");
Require(a.EarliestProgressionLevel == 1, "item-a earliest progression level mismatch.");
Require(!a.SingleSourceDominated, "item-a should not be single-source dominated.");
Require(a.ProvenanceClasses.SequenceEqual(new[] { "ModAdded", "PristineUnchanged" }), "item-a provenance ordering mismatch.");
Require(a.Channels.Single(channel => channel.Channel == AcquisitionChannel.Craft).RenewableSourceCount == 1, "craft channel summary mismatch.");

var b = result.Single(item => item.ItemTemplateId == "item-b");
Require(b.SourceCount == 1, "item-b source count mismatch.");
Require(b.SingleSourceDominated, "item-b should be single-source dominated.");
Require(!b.HasRenewablePath, "item-b should have no renewable path.");
Require(b.EarliestProgressionLevel is null, "unknown progression level must remain unknown.");

var reversed = SourcePressureEvidenceAnalyzer.Analyze(input.Reverse());
Require(
    result.Select(item => item.ItemTemplateId).SequenceEqual(reversed.Select(item => item.ItemTemplateId)),
    "Output ordering must be deterministic regardless of input order."
);
Require(
    result.Select(item => item.SourceCount).SequenceEqual(reversed.Select(item => item.SourceCount)),
    "Source counts must be deterministic regardless of input order."
);

MustFail("empty item id", () => SourcePressureEvidenceAnalyzer.Analyze(new[]
{
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = " ",
        SourceId = "source",
        Channel = AcquisitionChannel.Other,
        Renewable = false,
        ProvenanceClass = "Unknown",
    },
}));

MustFail("empty source id", () => SourcePressureEvidenceAnalyzer.Analyze(new[]
{
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item",
        SourceId = " ",
        Channel = AcquisitionChannel.Other,
        Renewable = false,
        ProvenanceClass = "Unknown",
    },
}));

MustFail("invalid progression level", () => SourcePressureEvidenceAnalyzer.Analyze(new[]
{
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item",
        SourceId = "source",
        Channel = AcquisitionChannel.Other,
        Renewable = false,
        EarliestProgressionLevel = 0,
        ProvenanceClass = "Unknown",
    },
}));

MustFail("empty provenance", () => SourcePressureEvidenceAnalyzer.Analyze(new[]
{
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item",
        SourceId = "source",
        Channel = AcquisitionChannel.Other,
        Renewable = false,
        ProvenanceClass = " ",
    },
}));

Console.WriteLine("Economy Admiral source pressure smoke PASS");
