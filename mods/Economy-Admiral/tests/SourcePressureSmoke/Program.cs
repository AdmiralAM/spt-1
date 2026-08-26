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
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item-c",
        SourceId = "trader-c:offer-1",
        Channel = AcquisitionChannel.TraderPurchase,
        Renewable = true,
        EarliestProgressionLevel = 1,
        ProvenanceClass = "ModAdded",
    },
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item-c",
        SourceId = "trader-c:offer-2",
        Channel = AcquisitionChannel.TraderPurchase,
        Renewable = true,
        EarliestProgressionLevel = 2,
        ProvenanceClass = "ModAdded",
    },
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item-c",
        SourceId = "quest-c",
        Channel = AcquisitionChannel.QuestReward,
        Renewable = false,
        EarliestProgressionLevel = 4,
        ProvenanceClass = "ModAdded",
    },
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item-d",
        SourceId = "craft-d",
        Channel = AcquisitionChannel.Craft,
        Renewable = true,
        EarliestProgressionLevel = 6,
        ProvenanceClass = "ModAdded",
    },
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item-d",
        SourceId = "quest-d",
        Channel = AcquisitionChannel.QuestReward,
        Renewable = false,
        EarliestProgressionLevel = 3,
        ProvenanceClass = "ModAdded",
    },
};

var result = SourcePressureEvidenceAnalyzer.Analyze(input);
Require(result.Count == 4, "Expected four item summaries.");

var a = result.Single(item => item.ItemTemplateId == "item-a");
Require(a.SourceCount == 3, "item-a source count should deduplicate identical edges.");
Require(a.ChannelCount == 3, "item-a should expose three independent channels.");
Require(a.RenewableSourceCount == 2, "item-a renewable count mismatch.");
Require(a.OneTimeSourceCount == 1, "item-a one-time count mismatch.");
Require(a.RenewableSourceShare == 0.666667, "item-a renewable share mismatch.");
Require(a.HasRenewablePath, "item-a should retain renewable acquisition.");
Require(a.RenewableChannelCount == 2, "item-a renewable channel count mismatch.");
Require(!a.SingleRenewableSourceRisk, "item-a has two renewable sources and should not expose single-renewable risk.");
Require(a.EarliestProgressionLevel == 1, "item-a earliest progression level mismatch.");
Require(!a.SingleSourceDominated, "item-a should not be single-source dominated.");
Require(a.DominantChannel == AcquisitionChannel.TraderPurchase, "item-a dominant channel tie must resolve deterministically by enum order.");
Require(a.DominantChannelSourceShare == 0.333333, "item-a dominant channel share mismatch.");
Require(a.ChannelConcentrationHhi == 0.333333, "item-a channel HHI mismatch.");
Require(a.EffectiveChannelCount == 3.0, "item-a effective channel count mismatch.");
Require(a.ProvenanceClasses.SequenceEqual(new[] { "ModAdded", "PristineUnchanged" }), "item-a provenance ordering mismatch.");
Require(a.Channels.Single(channel => channel.Channel == AcquisitionChannel.Craft).RenewableSourceCount == 1, "craft channel summary mismatch.");

var b = result.Single(item => item.ItemTemplateId == "item-b");
Require(b.SourceCount == 1, "item-b source count mismatch.");
Require(b.SingleSourceDominated, "item-b should be single-source dominated.");
Require(!b.HasRenewablePath, "item-b should have no renewable path.");
Require(b.RenewableChannelCount == 0, "item-b renewable channel count mismatch.");
Require(!b.SingleRenewableSourceRisk, "zero renewable sources is not the single-renewable-source state.");
Require(b.EarliestProgressionLevel is null, "unknown progression level must remain unknown.");
Require(b.DominantChannel == AcquisitionChannel.QuestReward, "item-b dominant channel mismatch.");
Require(b.DominantChannelSourceShare == 1.0, "item-b dominant channel share mismatch.");
Require(b.ChannelConcentrationHhi == 1.0, "item-b channel HHI mismatch.");
Require(b.EffectiveChannelCount == 1.0, "item-b effective channel count mismatch.");

var c = result.Single(item => item.ItemTemplateId == "item-c");
Require(c.SourceCount == 3, "item-c source count mismatch.");
Require(c.ChannelCount == 2, "item-c channel count mismatch.");
Require(c.RenewableChannelCount == 1, "item-c renewable channels mismatch.");
Require(c.DominantChannel == AcquisitionChannel.TraderPurchase, "item-c dominant channel mismatch.");
Require(c.DominantChannelSourceShare == 0.666667, "item-c channel concentration mismatch.");
Require(c.ChannelConcentrationHhi == 0.555556, "item-c channel HHI mismatch.");
Require(c.EffectiveChannelCount == 1.8, "item-c effective channel count mismatch.");

var d = result.Single(item => item.ItemTemplateId == "item-d");
Require(d.SourceCount == 2, "item-d source count mismatch.");
Require(d.RenewableSourceCount == 1, "item-d renewable source count mismatch.");
Require(d.RenewableChannelCount == 1, "item-d renewable channel count mismatch.");
Require(d.SingleRenewableSourceRisk, "item-d must expose a raw single-renewable-source risk state.");
Require(d.ChannelConcentrationHhi == 0.5, "item-d channel HHI mismatch.");
Require(d.EffectiveChannelCount == 2.0, "item-d effective channel count mismatch.");

var reversed = SourcePressureEvidenceAnalyzer.Analyze(input.Reverse());
Require(
    result.Select(item => item.ItemTemplateId).SequenceEqual(reversed.Select(item => item.ItemTemplateId)),
    "Output ordering must be deterministic regardless of input order."
);
Require(
    result.Select(item => item.SourceCount).SequenceEqual(reversed.Select(item => item.SourceCount)),
    "Source counts must be deterministic regardless of input order."
);
Require(
    result.Select(item => item.DominantChannelSourceShare).SequenceEqual(reversed.Select(item => item.DominantChannelSourceShare)),
    "Channel concentration must be deterministic regardless of input order."
);
Require(
    result.Select(item => item.ChannelConcentrationHhi).SequenceEqual(reversed.Select(item => item.ChannelConcentrationHhi)),
    "Channel HHI must be deterministic regardless of input order."
);
Require(
    result.Select(item => item.SingleRenewableSourceRisk).SequenceEqual(reversed.Select(item => item.SingleRenewableSourceRisk)),
    "Renewable resilience evidence must be deterministic regardless of input order."
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

MustFail("conflicting duplicate source identity", () => SourcePressureEvidenceAnalyzer.Analyze(new[]
{
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item",
        SourceId = "same-source",
        Channel = AcquisitionChannel.TraderPurchase,
        Renewable = true,
        EarliestProgressionLevel = 1,
        ProvenanceClass = "ModAdded",
    },
    new AcquisitionSourceEvidence
    {
        ItemTemplateId = "item",
        SourceId = "same-source",
        Channel = AcquisitionChannel.TraderPurchase,
        Renewable = false,
        EarliestProgressionLevel = 1,
        ProvenanceClass = "ModAdded",
    },
}));

Console.WriteLine("Economy Admiral source pressure smoke PASS");
