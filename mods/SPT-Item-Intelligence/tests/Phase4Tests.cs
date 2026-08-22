using System;
using System.Collections.Generic;
using SPTItemIntelligence;

public static class Phase4Tests
{
    static int assertions;

    public static int Run()
    {
        CurrentQuestProjection();
        CompletedContribution();
        FutureAndHideoutPolicies();
        ExplicitCombinationModes();
        SurplusCalculation();
        StoreRetentionAndReplacement();
        FoundInRaidReason();
        UnknownLookup();
        return assertions;
    }

    static void CurrentQuestProjection()
    {
        RequirementIndex index = Build(
            100,
            new[] { new OwnedTemplateCount("  A  ", 1) },
            new[] { new RequirementContribution("A", RequirementSource.CurrentQuest, 5, 2) });
        RequirementIndexEntry entry = index.GetNormalized("a");
        Expect(entry.TemplateId == "a", "template id normalized once during construction");
        Expect(entry.QuestNeededNow == 3, "active quest remaining count");
        Expect(entry.KeepCount == 3 && entry.SurplusCount == 0, "active quest keep and surplus");
        Expect((entry.Reasons & RequirementReasonFlags.CurrentQuest) != 0, "active quest reason flag");
    }

    static void CompletedContribution()
    {
        RequirementIndex index = Build(
            101,
            new[] { new OwnedTemplateCount("done", 2) },
            new[] { new RequirementContribution("done", RequirementSource.CurrentQuest, 5, 5) });
        RequirementIndexEntry entry = index.Get("DONE");
        Expect(entry.QuestNeededNow == 0 && entry.KeepCount == 0, "completed requirement contributes zero");
        Expect(entry.SurplusCount == 2 && entry.Reasons == RequirementReasonFlags.None, "completed reason is not published");
    }

    static void FutureAndHideoutPolicies()
    {
        RequirementProjection projection = new RequirementProjection(
            102,
            null,
            new[]
            {
                new RequirementContribution("future", RequirementSource.FutureQuest, 4),
                new RequirementContribution("hideout", RequirementSource.Hideout, 3)
            });
        RequirementIndex enabled = RequirementIndexBuilder.Build(projection);
        Expect(enabled.Get("future").QuestNeededLater == 4, "future quest remains advisory and separate");
        Expect(enabled.Get("hideout").HideoutNeeded == 3, "hideout count is separate");

        RequirementIndex disabled = RequirementIndexBuilder.Build(projection, new RequirementIndexOptions
        {
            IncludeFutureQuests = false,
            IncludeHideout = false
        });
        Expect(disabled.Count == 0, "future and hideout sources can be disabled before publication");
    }

    static void ExplicitCombinationModes()
    {
        RequirementIndex index = Build(
            103,
            null,
            new[]
            {
                new RequirementContribution("mixed", RequirementSource.CurrentQuest, 2),
                new RequirementContribution("mixed", RequirementSource.Hideout, 3),
                new RequirementContribution("mixed", RequirementSource.FutureQuest, 4, combineMode: RequirementCombineMode.AlternativeMaximum, alternativeGroup: "path"),
                new RequirementContribution("mixed", RequirementSource.FutureQuest, 7, satisfiedCount: 2, combineMode: RequirementCombineMode.AlternativeMaximum, alternativeGroup: "path")
            });
        RequirementIndexEntry entry = index.Get("mixed");
        Expect(entry.QuestNeededNow == 2 && entry.HideoutNeeded == 3 && entry.QuestNeededLater == 9, "source facts preserve raw outstanding totals");
        Expect(entry.KeepCount == 10, "additive sources plus maximum alternative contribution");

        bool rejected = false;
        try { new RequirementContribution("bad", RequirementSource.FutureQuest, 1, combineMode: RequirementCombineMode.AlternativeMaximum); }
        catch (ArgumentException) { rejected = true; }
        Expect(rejected, "alternative mode requires explicit stable group");
    }

    static void SurplusCalculation()
    {
        RequirementIndexEntry entry = Build(
            104,
            new[] { new OwnedTemplateCount("surplus", 8) },
            new[] { new RequirementContribution("surplus", RequirementSource.Hideout, 3) }).Get("surplus");
        Expect(entry.OwnedCount == 8 && entry.KeepCount == 3, "owned and keep counts retained");
        Expect(entry.SurplusCount == 5, "surplus is exact owned minus keep");
    }

    static void StoreRetentionAndReplacement()
    {
        object table = new object();
        RequirementIndexStore store = new RequirementIndexStore();
        FakeProjector first = new FakeProjector(new RequirementProjection(
            200,
            new[] { new OwnedTemplateCount("old", 1) },
            new[] { new RequirementContribution("old", RequirementSource.CurrentQuest, 2) }));

        string error;
        bool published = store.TryRefresh(new RequirementDataEnvelope(200, new object(), table, table), first, null, out error);
        Expect(published && error == null, "valid snapshot publishes");
        RequirementIndex old = store.Current;
        Expect(old.GeneratedAtUnixSeconds == 200 && old.Get("old").KeepCount == 2, "first generation is coherent");

        bool waiting = store.TryRefresh(new RequirementDataEnvelope(201, null, table, table), first, null, out error);
        Expect(!waiting && error == "Profile is not ready.", "profile-not-ready is a retained-state result");
        Expect(object.ReferenceEquals(old, store.Current), "profile-not-ready retains last valid index");

        FakeProjector second = new FakeProjector(new RequirementProjection(
            300,
            new[] { new OwnedTemplateCount("new", 5) },
            new[] { new RequirementContribution("new", RequirementSource.Hideout, 1) }));
        published = store.TryRefresh(new RequirementDataEnvelope(300, new object(), table, table), second, null, out error);
        RequirementIndex replacement = store.Current;
        Expect(published && !object.ReferenceEquals(old, replacement), "successful refresh atomically replaces generation");
        Expect(replacement.Get("old").KeepCount == 0 && replacement.Get("new").SurplusCount == 4, "replacement has no mixed old/new counts");

        RequirementIndex beforeFailure = store.Current;
        published = store.TryRefresh(new RequirementDataEnvelope(301, new object(), table, table), new ThrowingProjector(), null, out error);
        Expect(!published && error == "projection failed", "projection failure is surfaced");
        Expect(object.ReferenceEquals(beforeFailure, store.Current), "projection failure retains last valid index");
    }

    static void FoundInRaidReason()
    {
        RequirementIndexEntry entry = Build(
            400,
            null,
            new[] { new RequirementContribution("fir", RequirementSource.CurrentQuest, 2, foundInRaidRequired: true) }).Get("fir");
        Expect(entry.RequiresFoundInRaid, "FIR requirement remains explicit");
        Expect((entry.Reasons & RequirementReasonFlags.CurrentQuest) != 0, "FIR does not erase quest source");
    }

    static void UnknownLookup()
    {
        RequirementIndex index = Build(500, null, null);
        RequirementIndexEntry a = index.GetNormalized("unknown");
        RequirementIndexEntry b = index.GetNormalized("another");
        Expect(object.ReferenceEquals(a, b), "unknown lookup reuses zero entry");
        Expect(a.KeepCount == 0 && a.OwnedCount == 0 && a.Reasons == RequirementReasonFlags.None, "unknown lookup is zero requirement");
    }

    static RequirementIndex Build(long generation, IEnumerable<OwnedTemplateCount> owned, IEnumerable<RequirementContribution> contributions)
    {
        return RequirementIndexBuilder.Build(new RequirementProjection(generation, owned, contributions));
    }

    static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
    }

    sealed class FakeProjector : IRequirementDataProjector
    {
        readonly RequirementProjection projection;
        public FakeProjector(RequirementProjection projection) { this.projection = projection; }
        public RequirementProjection Project(RequirementDataEnvelope snapshot) { return projection; }
    }

    sealed class ThrowingProjector : IRequirementDataProjector
    {
        public RequirementProjection Project(RequirementDataEnvelope snapshot) { throw new InvalidOperationException("projection failed"); }
    }
}
