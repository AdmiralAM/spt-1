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
                        ["2"] = Stage("D", 6),
                        ["3"] = Stage("D", 5)
                    }
                },
                new Dictionary<string, object>
                {
                    ["type"] = "9",
                    ["stages"] = new Dictionary<string, object>
                    {
                        ["1"] = Stage("F", 2, true)
                    }
                }
            }
        };

        object[] prices =
        {
            new ItemPriceSnapshotEntry("A", 1000, "Therapist", 2000, 500, 2, 1)
        };
        Dictionary<string, object> hideoutProgress = new Dictionary<string, object>
        {
            ["areaProgresses"] = new Dictionary<string, object>
            {
                ["2"] = new Dictionary<string, object> { ["D"] = 4 }
            }
        };
        RequirementDataEnvelope envelope = new RequirementDataEnvelope(123, profile, quests, hideout, prices, hideoutProgress);
        RequirementProjection projection = new SptRequirementDataProjector().Project(envelope);
        Expect(projection.Owned.Count == 2, "owned templates projected", ref assertions);
        RequirementIndex index = RequirementIndexBuilder.Build(projection);
        Expect(index.Get("a").OwnedCount == 3, "stack count projected", ref assertions);
        Expect(index.Get("a").QuestNeededNow == 2, "current quest projected", ref assertions);
        Expect(index.Get("a").RequiresFoundInRaid, "FIR flag projected", ref assertions);
        Expect(index.Get("b").QuestNeededNow == 0 && index.Get("b").QuestNeededLater == 0, "completed quest ignored", ref assertions);
        Expect(index.Get("c").QuestNeededLater == 4, "future quest projected", ref assertions);
        Expect(index.Get("d").HideoutNeeded == 7, "deposited Hideout In Progress items reduce the current stage but not a future stage", ref assertions);
        Expect(index.Get("d").OwnedCount == 0, "deposited items are committed and never returned to shared owned inventory", ref assertions);
        Expect(index.Get("f").Allocation.HideoutFirRequired == 2 && index.Get("f").RequiresFoundInRaid,
            "native hideout isSpawnedInSession projects as an FIR-only requirement", ref assertions);
        RequirementIndex committedCurrent = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(
            new RequirementDataEnvelope(124,
                ProfileWithOwned("T", 3, 3, "20", 0),
                new object[0],
                HideoutWithCommittedAndFutureRequirement(),
                new object[0],
                new Dictionary<string, object>
                {
                    ["areaProgresses"] = new Dictionary<string, object>
                    {
                        ["20"] = new Dictionary<string, object> { ["T"] = 5 }
                    }
                })));
        Expect(committedCurrent.Get("t").KeepCount == 3 && committedCurrent.Get("t").Allocation.HideoutFirRequired == 3 &&
               committedCurrent.Get("t").Allocation.HideoutMissing == 0,
            "components committed to the current hideout stage remain satisfied while FIR stock is reserved for a future stage", ref assertions);
        RequirementIndex powerCord = RequirementIndexBuilder.Build(new SptRequirementDataProjector().Project(
            PowerCordLiveStateEnvelope()));
        Expect(powerCord.Get("cable").KeepCount == 10 && powerCord.Get("cable").HideoutNeeded == 10,
            "power cord aggregates only the two unfinished future stations after current queued upgrades are satisfied", ref assertions);
        Expect(powerCord.Get("cable").OwnedCount == 1 && powerCord.Get("cable").Allocation.HideoutFirRequired == 10 &&
               powerCord.Get("cable").Allocation.HideoutMissing == 9,
            "power cord truth remains one FIR owned, ten FIR required, nine missing", ref assertions);
        Expect(index.Get("old") == RequirementIndexEntry.Empty, "completed hideout stage ignored", ref assertions);

        ItemPresentationStore store = new ItemPresentationStore();
        RecordingSink sink = new RecordingSink();
        RequirementRuntimeBootstrap bootstrap = null;
        ItemHoverRuntimeController controller = new ItemHoverRuntimeController(store, sink, null, id => bootstrap.CreateFallback(id));
        bootstrap = new RequirementRuntimeBootstrap(new FixedTransport("snapshot"), new FixedDecoder(envelope), new SptRequirementDataProjector(), store, controller);
        ItemHoverText loading = controller.OnHoverEnter("missing");
        Expect(loading.Status == "Loading item data" && loading.DataState == ItemDataState.Loading, "loading fallback is visible and typed", ref assertions);
        string error;
        Expect(bootstrap.TryRefresh(CancellationToken.None, out error), "bootstrap succeeds", ref assertions);
        Expect(error == null && bootstrap.State == RequirementBootstrapState.Ready, "bootstrap publishes ready state", ref assertions);
        Expect(store.Get("a").Requirement.KeepCount == 2, "presentation store populated", ref assertions);
        Expect(store.Get("a").Price.BestSource == PriceSource.Flea && store.Get("a").Price.TotalValue == 2000, "live flea/trader snapshot populates Value", ref assertions);
        ItemHoverText active = controller.OnHoverEnter("a");
        Expect(active.Primary == "1,000 ₽ · Therapist" && active.Secondary == "Flea: 2,000 ₽", "live cached vendor and alternate flea values reach hover formatting", ref assertions);
        Expect(active.Status.Length == 0 && active.QuestNowLine == "For active quest: 0/2 · FIR 0/2", "non-FIR stock does not fulfill live FIR-only requirements", ref assertions);
        ItemHoverText missingHideout = controller.OnHoverEnter("d");
        Expect(missingHideout.HideoutLine == "For hideout after quests: 0/7" && ItemMarkerPresentation.From(missingHideout).Kind == ItemMarkerKind.Hideout,
            "numeric server hideout requirement reaches runtime marker classification", ref assertions);
        ItemHoverText unknown = controller.OnHoverEnter("unknown");
        Expect(unknown.SummaryLine == "Not Needed" && unknown.DataState == ItemDataState.Ready && !unknown.IsDiagnostic,
            "a successfully indexed item with no requirement has the authoritative Not Needed state", ref assertions);

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

    static Dictionary<string, object> Stage(string templateId, int count, bool isSpawnedInSession = false)
    {
        return new Dictionary<string, object>
        {
            ["requirements"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = 1, ["templateId"] = templateId, ["count"] = count,
                    ["isSpawnedInSession"] = isSpawnedInSession
                }
            }
        };
    }

    static Dictionary<string, object> ProfileWithOwned(string templateId, int count, int fir, string areaType, int areaLevel)
    {
        object[] items = new object[count];
        for (int i = 0; i < count; i++)
            items[i] = new Dictionary<string, object>
            {
                ["_tpl"] = templateId,
                ["upd"] = new Dictionary<string, object> { ["SpawnedInSession"] = i < fir }
            };
        return new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object> { ["items"] = items },
            ["Hideout"] = new Dictionary<string, object>
            {
                ["Areas"] = new object[] { new Dictionary<string, object> { ["type"] = areaType, ["level"] = areaLevel } }
            }
        };
    }

    static Dictionary<string, object> HideoutWithCommittedAndFutureRequirement()
    {
        return new Dictionary<string, object>
        {
            ["areas"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "20",
                    ["stages"] = new Dictionary<string, object>
                    {
                        ["1"] = Stage("T", 5, true),
                        ["2"] = Stage("other", 1),
                        ["3"] = Stage("T", 3, true)
                    }
                }
            }
        };
    }

    static RequirementDataEnvelope PowerCordLiveStateEnvelope()
    {
        object Area(string type, int level) => new Dictionary<string, object> { ["type"] = type, ["level"] = level };
        object RequirementArea(string type, string stage, int count) => new Dictionary<string, object>
        {
            ["type"] = type,
            ["stages"] = new Dictionary<string, object> { [stage] = Stage("CABLE", count, true) }
        };
        Dictionary<string, object> profile = new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object>
            {
                ["items"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["_tpl"] = "CABLE",
                        ["upd"] = new Dictionary<string, object> { ["SpawnedInSession"] = true }
                    }
                }
            },
            ["Hideout"] = new Dictionary<string, object>
            {
                ["Areas"] = new object[] { Area("8", 2), Area("9", 3), Area("11", 1), Area("12", 1), Area("16", 0), Area("20", 0) }
            }
        };
        Dictionary<string, object> hideout = new Dictionary<string, object>
        {
            ["areas"] = new object[]
            {
                RequirementArea("8", "1", 1),
                RequirementArea("9", "3", 3),
                RequirementArea("11", "2", 7),
                RequirementArea("12", "3", 5),
                RequirementArea("16", "3", 5),
                RequirementArea("20", "1", 10)
            }
        };
        Dictionary<string, object> progress = new Dictionary<string, object>
        {
            ["areaProgresses"] = new Dictionary<string, object>
            {
                ["11"] = new Dictionary<string, object> { ["CABLE"] = 7 },
                ["20"] = new Dictionary<string, object> { ["CABLE"] = 10 }
            }
        };
        return new RequirementDataEnvelope(200, profile, Array.Empty<object>(), hideout, Array.Empty<object>(), progress);
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

