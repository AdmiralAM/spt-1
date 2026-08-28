using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class ReplacementComparabilitySmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var items = SourcePressureEvidenceAnalyzer.Analyze(new[]
        {
            new AcquisitionSourceEvidence { ItemTemplateId = "subject", SourceId = "t", Channel = AcquisitionChannel.TraderPurchase, Renewable = true, EarliestProgressionLevel = 5, ProvenanceClass = "ModAdded" },
            new AcquisitionSourceEvidence { ItemTemplateId = "subject", SourceId = "c", Channel = AcquisitionChannel.Craft, Renewable = true, EarliestProgressionLevel = 8, ProvenanceClass = "ModAdded" },
            new AcquisitionSourceEvidence { ItemTemplateId = "candidate", SourceId = "t2", Channel = AcquisitionChannel.TraderPurchase, Renewable = true, EarliestProgressionLevel = 7, ProvenanceClass = "ModAdded" },
            new AcquisitionSourceEvidence { ItemTemplateId = "candidate", SourceId = "q", Channel = AcquisitionChannel.QuestReward, Renewable = false, EarliestProgressionLevel = null, ProvenanceClass = "ModAdded" },
            new AcquisitionSourceEvidence { ItemTemplateId = "one-time", SourceId = "q2", Channel = AcquisitionChannel.QuestReward, Renewable = false, EarliestProgressionLevel = null, ProvenanceClass = "ModAdded" },
        });

        var facts = new[]
        {
            new ReplacementCandidateFact { SubjectItemId = "subject", CandidateItemId = "candidate", Relationship = "ExplicitTestRelation" },
            new ReplacementCandidateFact { SubjectItemId = "subject", CandidateItemId = "candidate", Relationship = "ExplicitTestRelation" },
            new ReplacementCandidateFact { SubjectItemId = "subject", CandidateItemId = "one-time", Relationship = "ExplicitFallback" },
        };
        var evidence = ReplacementComparabilityEvidenceAnalyzer.Analyze(items, facts);
        Require(evidence.Count == 2, "identical replacement facts must deduplicate");
        var comparable = evidence.Single(x => x.CandidateItemId == "candidate");
        Require(comparable.SubjectHasRenewablePath && comparable.CandidateHasRenewablePath, "renewable path flags mismatch");
        Require(comparable.SubjectRenewableChannelCount == 2 && comparable.CandidateRenewableChannelCount == 1, "renewable channel counts mismatch");
        Require(comparable.SubjectEarliestRenewableProgressionLevel == 5 && comparable.CandidateEarliestRenewableProgressionLevel == 7, "renewable progression levels mismatch");
        Require(comparable.RenewableProgressionLevelDelta == 2, "renewable progression delta mismatch");
        Require(comparable.ChannelIntersection.SequenceEqual(new[] { AcquisitionChannel.TraderPurchase }), "channel intersection mismatch");
        Require(comparable.SubjectOnlyChannels.SequenceEqual(new[] { AcquisitionChannel.Craft }), "subject-only channels mismatch");
        Require(comparable.CandidateOnlyChannels.Count == 0, "candidate-only channels mismatch");
        Require(comparable.ChannelJaccardOverlap == 0.5, "Jaccard overlap mismatch");
        Require(!comparable.CandidateHasCompleteProgressionEvidence, "candidate progression coverage must remain incomplete");

        var oneTime = evidence.Single(x => x.CandidateItemId == "one-time");
        Require(!oneTime.CandidateHasRenewablePath && oneTime.CandidateEarliestRenewableProgressionLevel is null, "non-renewable candidate must preserve unknown renewable progression");
        Require(oneTime.RenewableProgressionLevelDelta is null, "missing progression must remain null");

        var reversed = ReplacementComparabilityEvidenceAnalyzer.Analyze(items.Reverse(), facts.Reverse());
        Require(evidence.SequenceEqual(reversed), "replacement output must be deterministic under reverse input");

        MustFail(() => ReplacementComparabilityEvidenceAnalyzer.Analyze(items, new[] { new ReplacementCandidateFact { SubjectItemId = "subject", CandidateItemId = "subject", Relationship = "bad" } }));
        MustFail(() => ReplacementComparabilityEvidenceAnalyzer.Analyze(items, new[] { new ReplacementCandidateFact { SubjectItemId = "missing", CandidateItemId = "candidate", Relationship = "bad" } }));
        MustFail(() => ReplacementComparabilityEvidenceAnalyzer.Analyze(items, new[]
        {
            new ReplacementCandidateFact { SubjectItemId = "subject", CandidateItemId = "candidate", Relationship = "A" },
            new ReplacementCandidateFact { SubjectItemId = "subject", CandidateItemId = "candidate", Relationship = "B" },
        }));
        Console.WriteLine("Economy Admiral replacement comparability smoke PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void MustFail(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected replacement comparability operation to fail closed.");
    }
}
