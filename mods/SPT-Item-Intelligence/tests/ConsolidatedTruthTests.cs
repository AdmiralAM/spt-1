using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class ConsolidatedTruthTests
{
    static int assertions;
    public static int Run()
    {
        // Exhaustive small stocks prove conservation and the optimal total shortfall.
        for (int owned = 0; owned <= 8; owned++)
        for (int fir = 0; fir <= owned; fir++)
        for (int now = 0; now <= 3; now++)
        for (int later = 0; later <= 3; later++)
        for (int hideout = 0; hideout <= 3; hideout++)
        for (int nowFir = 0; nowFir <= now; nowFir++)
        for (int laterFir = 0; laterFir <= later; laterFir++)
        {
            var a = new ItemRequirementAllocation(owned, fir, now, later, hideout, nowFir, laterFir);
            int total = now + later + hideout;
            int minimumMissing = Math.Max(Math.Max(0, total - owned), Math.Max(0, nowFir + laterFir - fir));
            Check(a.Missing == minimumMissing, "minimum feasible shortfall");
            Check(a.NowAllocated + a.LaterAllocated + a.HideoutAllocated + a.Surplus == owned, "no owned unit reused");
            Check(a.NowFirAllocated + a.LaterFirAllocated == Math.Min(fir, nowFir + laterFir), "FIR obligations receive stock first");
            Check(a.Missing == a.NowMissing + a.LaterMissing + a.HideoutMissing, "source deficits reconcile");
            Check(a.Keep == total && a.KeepOwned <= a.Keep, "keep total and owned retention differ");
        }
        var critical = new ItemRequirementAllocation(5, 2, 2, 2, 2, 0, 2);
        Check(critical.LaterFirAllocated == 2 && critical.NowAllocated == 2 && critical.HideoutAllocated == 1, "future FIR protected before active any/hideout");
        Check(critical.Coverage == RequirementCoverage.NeedMore && critical.MustKeep, "Need More still retains stock");
        Check(new ItemRequirementAllocation(4, 2, 2, 2, 0, 2, 0).Coverage == RequirementCoverage.Enough, "Enough");
        Check(new ItemRequirementAllocation(4, 2, 0, 0, 0, 0, 0).Coverage == RequirementCoverage.NotNeeded, "Not Needed");
        var contributions = new[] {
            new RequirementContribution("x", RequirementSource.CurrentQuest, 3, 1, true, label: "Active"),
            new RequirementContribution("x", RequirementSource.FutureQuest, 2, label: "Same display name"),
            new RequirementContribution("x", RequirementSource.FutureQuest, 4, label: "Same display name"),
            new RequirementContribution("x", RequirementSource.Hideout, 3, label: "Workbench L2") };
        var stock = new[] { new OwnedTemplateCount("x", 5, 2) };
        var index = RequirementIndexBuilder.Build(new RequirementProjection(1, stock, contributions));
        var entry = index.Get("x");
        Check(entry.KeepCount == 11 && entry.QuestNeededLater == 6 && entry.Allocation.Missing == 6, "independent future obligations plus partial handover");
        Array.Reverse(contributions);
        var reversed = RequirementIndexBuilder.Build(new RequirementProjection(1, stock, contributions)).Get("x");
        Check(reversed.Allocation.Missing == entry.Allocation.Missing && reversed.Allocation.NowAllocated == entry.Allocation.NowAllocated, "input order invariance");
        var disabled = RequirementIndexBuilder.Build(new RequirementProjection(1, stock, contributions), new RequirementIndexOptions { IncludeCurrentQuests = false, IncludeFutureQuests = false, IncludeHideout = false });
        Check(disabled.Get("x").KeepCount == 0, "disabled sources create no demand");
        var state = ItemRequirementStateBuilder.Build(index).Get("x");
        Check(object.ReferenceEquals(entry.Allocation, state.Allocation), "truth preserved across state publication");
        var presentation = ItemPresentationIndexBuilder.Build(ItemRequirementStateBuilder.Build(index), null).Get("x");
        FirRequirementRegistry.Publish(new Dictionary<string, FirRequirementState> { ["x"] = new FirRequirementState(500, 500, 500) });
        var text = new ItemHoverTextFormatter().Format(new ItemHoverState(presentation));
        Check(object.ReferenceEquals(entry.Allocation, text.Allocation) && text.OwnedFoundInRaid == 2, "tooltip uses its own snapshot, not side registry");
        FirRequirementRegistry.Clear();
        HideoutProjection();
        QuestProjection();
        ModulesAndPalette();
        DisabledBootstrap();
        Console.WriteLine("Consolidated truth: " + assertions + " assertions passed.");
        return assertions;
    }
    static void HideoutProjection()
    {
        var station = D("type", 10, "stages", D("1", Stage(100), "2", Stage(2), "3", Stage(3)));
        var profile = D("Inventory", D("items", new object[] { D("_tpl", "x", "upd", D("StackObjectsCount", 4, "SpawnedInSession", true)) }),
            "Quests", new object[0], "Hideout", D("Areas", new object[] { D("type", 10, "level", 1) }));
        var envelope = new RequirementDataEnvelope(1, profile, D(), D("areas", new object[] { station, station }, "customAreas", new object[] { station }));
        var entry = RequirementIndexBuilder.Build(new AqcQuestRequirementProjector().Project(envelope)).Get("x");
        Check(entry.HideoutNeeded == 5 && entry.Details.Count == 2, "same station/level across tables counted once; completed excluded");
        Check(entry.Details[0].Label.Contains("L2 (current)") && entry.Details[1].Label.Contains("L3 (future)"), "current/future station levels visible");
        Check(entry.OwnedCount == 4 && entry.Allocation.OwnedFir == 4 && entry.Allocation.HideoutMissing == 1, "FIR stock and hideout deficit preserved");
    }
    static void QuestProjection()
    {
        var fir = D("id", "fir", "conditionType", "HandoverItem", "target", new[] { "x", "x" }, "value", 4, "onlyFoundInRaid", true);
        var any = D("id", "any", "conditionType", "HandoverItem", "target", "x", "value", 3);
        var find = D("id", "find", "conditionType", "FindItem", "target", "x", "value", 4, "onlyFoundInRaid", true);
        var place = D("id", "place", "conditionType", "LeaveItemAtLocation", "target", "x", "value", 2);
        var profile = D("Inventory", D("items", new object[0]), "Quests", new object[] { D("qid", "q", "status", "Started") },
            "TaskConditionCounters", D("counter-random-id", D("id", "counter-random-id", "sourceId", "fir", "value", 1),
                "unrelated-counter", D("sourceId", "unrelated", "value", 3)));
        var quests = D("q", D("_id", "q", "QuestName", "Quest", "conditions", D("AvailableForFinish", new object[] { fir, any, find, place, fir })));
        var e = RequirementIndexBuilder.Build(new AqcQuestRequirementProjector().Project(new RequirementDataEnvelope(1, profile, quests, D()))).Get("x");
        Check(e.QuestNeededNow == 8 && e.Allocation.NowFirRequired == 3, "partial handover, distinct consumption, duplicate condition/target and Find projection");
        Check(e.Details.Count == 3, "mixed FIR and any-item conditions stay explicit");
    }
    static void ModulesAndPalette()
    {
        for (int mask = 0; mask < 256; mask++)
        {
            var m = new ModuleSelection((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0,
                (mask & 16) != 0, (mask & 32) != 0, (mask & 64) != 0, (mask & 128) != 0);
            Check(m.Key == mask, "each F12 toggle remains independent");
            Check(!m.Backgrounds || (m.Prices && m.TrackViews), "background survives marker and tooltip disable");
            Check(m.Markers || m.Tooltips || m.Backgrounds || !m.AnyConsumer, "no presentation consumers means no data work");
            Check(m.Tooltips || !m.CraftBarter, "hidden relevance does no work");
        }
        Check(new ModuleSelection(markers: false).DataKey == ModuleSelection.Default.DataKey, "marker toggle with tooltip enabled needs no data refresh");
        Check(BackgroundPalette.Money(9999) == "" && BackgroundPalette.Money(10000) == "#526B3F", "money tint entry threshold");
        Check(BackgroundPalette.Money(24999) == "#526B3F" && BackgroundPalette.Money(25000) == "#294F31", "money tier boundary");
        Check(BackgroundPalette.Ammo(20) == "#526B3F" && BackgroundPalette.Ammo(21) == "#253552", "ammo uses penetration-specific tiers");
        Check(BackgroundPalette.Key(100000, false) == "#660415", "flea-banned key color takes precedence");
        Check(BackgroundPalette.HasDedicatedOwner(true, "default"), "key background yields to BetterKeys-style ownership");
        Check(!BackgroundPalette.HasDedicatedOwner(false, "default"), "neutral ordinary item remains eligible for valuation tint");
        Check(BackgroundPalette.Ammo(9) == "" && BackgroundPalette.Ammo(10) == "#526B3F", "legacy ammo tint starts at penetration ten");
        var irrelevant = new ItemHoverText("100 ₽", "", "");
        Check(!ItemMarkerPresentation.From(irrelevant, contextual: true).IsVisible, "no contextual marker on irrelevant value-only item");
        var need = new ItemHoverText("", "", "", "x", 0, 0, 0, 1, 1);
        Check(ItemMarkerPresentation.From(need, contextual: true).Kind == ItemMarkerKind.Hideout, "hideout attention is first class");
        var requirements = RequirementIndexBuilder.Build(new RequirementProjection(1, null, new[] { new RequirementContribution("x", RequirementSource.CurrentQuest, 1) }));
        var presentation = ItemPresentationIndexBuilder.Build(ItemRequirementStateBuilder.Build(requirements), ItemPriceIndexBuilder.Build(new[] { new ItemPriceInput("x", 10000) })).Get("x");
        var withoutValue = new ItemHoverTextFormatter().Format(new ItemHoverState(presentation), ItemValueMode.Vendor,
            new ModuleSelection(value: false));
        Check(withoutValue.ValueLine == "" && withoutValue.QuestNowLine.Length > 0, "value section disables without removing quest truth");
    }
    static void DisabledBootstrap()
    {
        var transport = new CountingTransport();
        var store = new ItemPresentationStore();
        var controller = new ItemHoverRuntimeController(store, new NullSink());
        var bootstrap = new RequirementRuntimeBootstrap(transport, new NeverDecoder(), new NeverProjector(), store, controller,
            modules: () => new ModuleSelection(false, false, false, false, false, false, false, false));
        string error;
        Check(bootstrap.TryRefresh(default(System.Threading.CancellationToken), out error), "all-disabled bootstrap succeeds without data");
        Check(transport.Calls == 0 && bootstrap.State == RequirementBootstrapState.Ready && bootstrap.Detail == "NO ENABLED MODULES", "all-disabled performs no transport/projector work");
    }
    static object Stage(int count) => D("requirements", new object[] { D("type", "Item", "templateId", "x", "count", count) });
    static Dictionary<string, object> D(params object[] pairs) { var d = new Dictionary<string, object>(); for (int i = 0; i < pairs.Length; i += 2) d[(string)pairs[i]] = pairs[i + 1]; return d; }
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new InvalidOperationException("Consolidated truth: " + message); }
    sealed class CountingTransport : IRequirementSnapshotTransport { public int Calls; public string GetSnapshotJson() { Calls++; return "unexpected"; } }
    sealed class NeverDecoder : IRequirementSnapshotDecoder { public RequirementDataEnvelope Decode(string json) { throw new InvalidOperationException("decoder must be idle"); } }
    sealed class NeverProjector : IRequirementDataProjector { public RequirementProjection Project(RequirementDataEnvelope snapshot) { throw new InvalidOperationException("projector must be idle"); } }
    sealed class NullSink : IItemHoverViewSink { public void Show(ItemHoverText text) { } public void Clear() { } }
}
