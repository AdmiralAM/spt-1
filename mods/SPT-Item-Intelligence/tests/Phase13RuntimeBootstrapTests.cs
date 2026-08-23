using System;
using System.Collections.Generic;
using System.Threading;
using SPTItemIntelligence;

static class Phase13RuntimeBootstrapTests
{
    public static int Run()
    {
        int assertions = 0;
        Dictionary<string, object> profile = new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object>
            {
                ["items"] = new object[]
                {
                    new Dictionary<string, object> { ["_tpl"] = "A", ["upd"] = new Dictionary<string, object> { ["StackObjectsCount"] = 3 } },
                    new Dictionary<string, object> { ["_tpl"] = "B" }
                }
            },
            ["Quests"] = new object[]
            {
                new Dictionary<string, object> { ["qid"] = "q1", ["status"] = "Started" },
                new Dictionary<string, object> { ["qid"] = "q2", ["status"] = "Success" }
            },
            ["Hideout"] = new Dictionary<string, object>
            {
                ["Areas"] = new object[] { new Dictionary<string, object> { ["type"] = "2", ["level"] = 1 } }
            }
        };
        Dictionary<string, object> quests = new Dictionary<string, object>
        {
            ["q1"] = Quest("q1", "A", 2, true),
            ["q2"] = Quest("q2", "B", 5, false),
            ["q3"] = Quest("q3", "C", 4, false)
        };
        Dictionary<string, object> hideout = new Dictionary<string, object>
        {
            ["areas"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "2",
                    ["stages"] = new Dictionary<string, object>
                    {
                        ["1"] = Stage("OLD", 9),
                        ["2"] = Stage("D", 6)
                    }
                }
            }
        };

        object[] prices =
        {
            new ItemPriceSnapshotEntry("A", 1000, "Therapist", 2000, 500, 2, 1)
        };
        RequirementDataEnvelope envelope = new RequirementDataEnvelope(123, profile, quests, hideout, prices);
        RequirementProjection projection = new SptRequirementDataProjector().Project(envelope);
        Expect(projection.Owned.Count == 2, "owned templates projected", ref assertions);
        RequirementIndex index = RequirementIndexBuilder.Build(projection);
        Expect(index.Get("a").OwnedCount == 3, "stack count projected", ref assertions);
        Expect(index.Get("a").QuestNeededNow == 2, "current quest projected", ref assertions);
        Expect(index.Get("a").RequiresFoundInRaid, "FIR flag projected", ref assertions);
        Expect(index.Get("b").QuestNeededNow == 0 && index.Get("b").QuestNeededLater == 0, "completed quest ignored", ref assertions);
        Expect(index.Get("c").QuestNeededLater == 4, "future quest projected", ref assertions);
        Expect(index.Get("d").HideoutNeeded == 6, "future hideout stage projected", ref assertions);
        Expect(index.Get("old") == RequirementIndexEntry.Empty, "completed hideout stage ignored", ref assertions);

        ItemPresentationStore store = new ItemPresentationStore();
        RecordingSink sink = new RecordingSink();
        RequirementRuntimeBootstrap bootstrap = null;
        ItemHoverRuntimeController controller = new ItemHoverRuntimeController(store, sink, null, id => bootstrap.CreateFallback(id));
        bootstrap = new RequirementRuntimeBootstrap(new FixedTransport("snapshot"), new FixedDecoder(envelope), new SptRequirementDataProjector(), store, controller);
        ItemHoverText loading = controller.OnHoverEnter("missing");
        Expect(loading.Status == "LOADING ITEM DATA", "loading fallback is visible", ref assertions);
        string error;
        Expect(bootstrap.TryRefresh(CancellationToken.None, out error), "bootstrap succeeds", ref assertions);
        Expect(error == null && bootstrap.State == RequirementBootstrapState.Ready, "bootstrap publishes ready state", ref assertions);
        Expect(store.Get("a").Requirement.KeepCount == 2, "presentation store populated", ref assertions);
        Expect(store.Get("a").Price.BestSource == PriceSource.Flea && store.Get("a").Price.TotalValue == 2000, "live flea/trader snapshot populates Value", ref assertions);
        ItemHoverText active = controller.OnHoverEnter("a");
        Expect(active.Primary == "2,000 ₽" && active.Secondary == "1,000 ₽/slot", "live Value and per-slot value reach hover formatting", ref assertions);
        Expect(active.Status.Length == 0 && active.QuestNowLine == "Quest Now: 2/2 ✓", "live requirement fulfillment reaches hover without sell labels", ref assertions);
        ItemHoverText unknown = controller.OnHoverEnter("unknown");
        Expect(unknown.Primary == "ITEM INTELLIGENCE" && unknown.Status == "NO REQUIREMENT DATA", "ready fallback is diagnostic", ref assertions);

        RequirementRuntimeBootstrap failed = null;
        ItemHoverRuntimeController failedController = new ItemHoverRuntimeController(new ItemPresentationStore(), sink, null, id => failed.CreateFallback(id));
        failed = new RequirementRuntimeBootstrap(new ThrowingTransport(), new FixedDecoder(envelope), new SptRequirementDataProjector(), new ItemPresentationStore(), failedController);
        Expect(!failed.TryRefresh(CancellationToken.None, out error), "transport failure is contained", ref assertions);
        Expect(failed.State == RequirementBootstrapState.Unavailable && error.Length > 0, "failure state is explicit", ref assertions);
        return assertions;
    }

    static Dictionary<string, object> Quest(string id, string target, int count, bool fir)
    {
        return new Dictionary<string, object>
        {
            ["_id"] = id,
            ["conditions"] = new Dictionary<string, object>
            {
                ["AvailableForFinish"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["conditionType"] = "HandoverItem", ["target"] = new object[] { target },
                        ["value"] = count, ["onlyFoundInRaid"] = fir
                    }
                }
            }
        };
    }

    static Dictionary<string, object> Stage(string templateId, int count)
    {
        return new Dictionary<string, object>
        {
            ["requirements"] = new object[]
            {
                new Dictionary<string, object> { ["type"] = "Item", ["templateId"] = templateId, ["count"] = count }
            }
        };
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 13 assertion failed: " + message);
    }

    sealed class FixedTransport : IRequirementSnapshotTransport
    {
        readonly string json;
        public FixedTransport(string json) { this.json = json; }
        public string GetSnapshotJson() { return json; }
    }

    sealed class ThrowingTransport : IRequirementSnapshotTransport
    {
        public string GetSnapshotJson() { throw new InvalidOperationException("offline"); }
    }

    sealed class FixedDecoder : IRequirementSnapshotDecoder
    {
        readonly RequirementDataEnvelope envelope;
        public FixedDecoder(RequirementDataEnvelope envelope) { this.envelope = envelope; }
        public RequirementDataEnvelope Decode(string json) { return envelope; }
    }

    sealed class RecordingSink : IItemHoverViewSink
    {
        public ItemHoverText Current = ItemHoverText.Empty;
        public void Show(ItemHoverText text) { Current = text; }
        public void Clear() { Current = ItemHoverText.Empty; }
    }
}
