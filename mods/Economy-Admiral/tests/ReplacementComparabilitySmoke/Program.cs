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

static IReadOnlyList<ItemSourcePressureEvidence> BuildItems()
{
    var sources = new[]
    {
        new AcquisitionSourceEvidence
        {
            ItemTemplateId = "subject-a",
            SourceId = "trader-a",
            Channel = AcquisitionChannel.TraderPurchase,
            Renewable = true,
            EarliestProgressionLevel = 5,
            ProvenanceClass = "PristineUnchanged",
        },
        new AcquisitionSourceEvidence
        {
            ItemTemplateId = "subject-a",
            SourceId = "quest-a",
            Channel = AcquisitionChannel.QuestReward,
            Renewable = false,
            EarliestProgressionLevel = 2,
            ProvenanceClass = "PristineUnchanged",
        },
        new AcquisitionSourceEvidence
        {
            ItemTemplateId = "candidate-a",
            SourceId = "trader-b",
            Channel = AcquisitionChannel.TraderPurchase,
            Renewable = true,
            EarliestProgressionLevel = 7,
            ProvenanceClass = "ModAdded",
        },
        new AcquisitionSourceEvidence
        {
            ItemTemplateId = "candidate-a",
            SourceId = "craft-b",
            Channel = AcquisitionChannel.Craft,
            Renewable = true,
            EarliestProgressionLevel = 8,
            ProvenanceClass = "ModAdded",
        },
        new AcquisitionSourceEvidence
        {
            ItemTemplateId = "candidate-b",
            SourceId = "quest-b",
            Channel = AcquisitionChannel.QuestReward,
            Renewable = false,
            EarliestProgressionLevel = 4,
            ProvenanceClass = "ModAdded",
        },
        new AcquisitionSourceEvidence
        {
            ItemTemplateId = "candidate-c",
            SourceId = "craft-c",
            Channel = AcquisitionChannel.Craft,
            Renewable = true,
            EarliestProgressionLevel = null,
            ProvenanceClass = "ModAdded",
        },
    };

    return SourcePressureEvidenceAnalyzer.Analyze(sources);
}

var items = BuildItems();
var relationships = new[]
{
    new ReplacementRelationshipEvidence
    {
        SubjectItemTemplateId = "subject-a",
        CandidateItemTemplateId = "candidate-a",
        RelationshipClass = "SameFunctionalFamily",
    },
    new ReplacementRelationshipEvidence
    {
        SubjectItemTemplateId = "subject-a",
        CandidateItemTemplateId = "candidate-b",
        RelationshipClass = "SameFunctionalFamily",
    },
    new ReplacementRelationshipEvidence
    {
        SubjectItemTemplateId = "subject-a",
        CandidateItemTemplateId = "candidate-c",
        RelationshipClass = "SameFunctionalFamily",
    },
    new ReplacementRelationshipEvidence
    {
        SubjectItemTemplateId = "subject-a",
        CandidateItemTemplateId = "candidate-a",
        RelationshipClass = "SameFunctionalFamily",
    },
};

var result = ReplacementComparabilityAnalyzer.Analyze(items, relationships);
Require(result.Count == 3, "Identical relationship duplicates must de-duplicate.");

var a = result.Single(item => item.CandidateItemTemplateId == "candidate-a");
Require(a.SubjectHasRenewablePath, "subject-a should have a renewable path.");
Require(a.CandidateHasRenewablePath, "candidate-a should have renewable paths.");
Require(a.SubjectRenewableChannelCount == 1, "subject renewable channel count mismatch.");
Require(a.CandidateRenewableChannelCount == 2, "candidate-a renewable channel count mismatch.");
Require(a.SubjectEarliestRenewableProgressionLevel == 5, "subject renewable progression mismatch.");
Require(a.CandidateEarliestRenewableProgressionLevel == 7, "candidate-a renewable progression mismatch.");
Require(a.RenewableProgressionLevelDelta == 2, "candidate-a raw progression delta must be +2.");
Require(a.HasKnownRenewableProgressionComparison, "candidate-a progression comparison should be known.");
Require(a.SubjectHasCompleteProgressionEvidence, "subject progression evidence should be complete.");
Require(a.CandidateHasCompleteProgressionEvidence, "candidate-a progression evidence should be complete.");
Require(a.SharedChannels.SequenceEqual([AcquisitionChannel.TraderPurchase]), "candidate-a shared channels mismatch.");
Require(a.SubjectOnlyChannels.SequenceEqual([AcquisitionChannel.QuestReward]), "candidate-a subject-only channels mismatch.");
Require(a.CandidateOnlyChannels.SequenceEqual([AcquisitionChannel.Craft]), "candidate-a candidate-only channels mismatch.");
Require(a.ChannelJaccardOverlap == 0.333333, "candidate-a channel Jaccard mismatch.");

var b = result.Single(item => item.CandidateItemTemplateId == "candidate-b");
Require(!b.CandidateHasRenewablePath, "candidate-b must remain explicitly non-renewable.");
Require(b.CandidateEarliestRenewableProgressionLevel is null, "candidate-b renewable progression must be unknown/null.");
Require(b.RenewableProgressionLevelDelta is null, "candidate-b must not coerce unknown progression to zero.");
Require(!b.HasKnownRenewableProgressionComparison, "candidate-b progression comparison must remain unknown.");
Require(b.SharedChannels.SequenceEqual([AcquisitionChannel.QuestReward]), "candidate-b shared channel mismatch.");
Require(b.ChannelJaccardOverlap == 0.5, "candidate-b channel Jaccard mismatch.");

var c = result.Single(item => item.CandidateItemTemplateId == "candidate-c");
Require(c.CandidateHasRenewablePath, "candidate-c should be renewable despite unknown progression gate.");
Require(c.CandidateProgressionEvidenceCoverage == 0, "candidate-c progression coverage should expose missing evidence.");
Require(!c.CandidateHasCompleteProgressionEvidence, "candidate-c progression evidence must be incomplete.");
Require(c.CandidateEarliestRenewableProgressionLevel is null, "candidate-c renewable progression should remain unknown.");
Require(c.RenewableProgressionLevelDelta is null, "candidate-c unknown progression must not become a numeric delta.");
Require(c.ChannelJaccardOverlap == 0, "candidate-c has no shared acquisition channel with subject-a.");

var reversed = ReplacementComparabilityAnalyzer.Analyze(items.Reverse(), relationships.Reverse());
Require(
    result.Select(item => (item.SubjectItemTemplateId, item.CandidateItemTemplateId, item.RelationshipClass, item.RenewableProgressionLevelDelta, item.ChannelJaccardOverlap))
        .SequenceEqual(reversed.Select(item => (item.SubjectItemTemplateId, item.CandidateItemTemplateId, item.RelationshipClass, item.RenewableProgressionLevelDelta, item.ChannelJaccardOverlap))),
    "Replacement comparability output must be ordering-independent."
);

MustFail("same subject and candidate", () => ReplacementComparabilityAnalyzer.Analyze(items,
[
    new ReplacementRelationshipEvidence
    {
        SubjectItemTemplateId = "subject-a",
        CandidateItemTemplateId = "subject-a",
        RelationshipClass = "SameFunctionalFamily",
    },
]));

MustFail("missing candidate evidence", () => ReplacementComparabilityAnalyzer.Analyze(items,
[
    new ReplacementRelationshipEvidence
    {
        SubjectItemTemplateId = "subject-a",
        CandidateItemTemplateId = "missing-item",
        RelationshipClass = "SameFunctionalFamily",
    },
]));

MustFail("conflicting relationship evidence", () => ReplacementComparabilityAnalyzer.Analyze(items,
[
    new ReplacementRelationshipEvidence
    {
        SubjectItemTemplateId = "subject-a",
        CandidateItemTemplateId = "candidate-a",
        RelationshipClass = "SameFunctionalFamily",
    },
    new ReplacementRelationshipEvidence
    {
        SubjectItemTemplateId = "subject-a",
        CandidateItemTemplateId = "candidate-a",
        RelationshipClass = "SameCaliberOnly",
    },
]));

MustFail("empty relationship class", () => ReplacementComparabilityAnalyzer.Analyze(items,
[
    new ReplacementRelationshipEvidence
    {
        SubjectItemTemplateId = "subject-a",
        CandidateItemTemplateId = "candidate-a",
        RelationshipClass = " ",
    },
]));

MustFail("duplicate item evidence", () => ReplacementComparabilityAnalyzer.Analyze(items.Concat([items[0]]), relationships));

Console.WriteLine("Economy Admiral replacement comparability smoke PASS");
