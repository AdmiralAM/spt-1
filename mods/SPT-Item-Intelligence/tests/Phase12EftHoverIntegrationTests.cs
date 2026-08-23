using System;
using System.Collections;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase12EftHoverIntegrationTests
{
    public static int Run()
    {
        int assertions = 0;

        Expect(EftItemTemplateIdResolver.Resolve(new DirectItemView
        {
            Item = new DirectItem { TemplateId = "  ABC  " }
        }) == "abc", "direct TemplateId resolves and normalizes", ref assertions);

        Expect(EftItemTemplateIdResolver.Resolve(new ObfuscatedItemView
        {
            _item = new ObfuscatedItem { _tpl = " DEF " }
        }) == "def", "obfuscated _item/_tpl shape resolves", ref assertions);

        Expect(EftItemTemplateIdResolver.Resolve(new NestedItemView
        {
            Item = new NestedItem { Template = new NestedTemplate { _id = " GHI " } }
        }) == "ghi", "nested template id resolves", ref assertions);

        Hashtable itemDictionary = new Hashtable(StringComparer.OrdinalIgnoreCase)
        {
            ["Item"] = new Hashtable(StringComparer.OrdinalIgnoreCase) { ["_tpl"] = " JKL " }
        };
        Expect(EftItemTemplateIdResolver.Resolve(itemDictionary) == "jkl", "dictionary snapshot shape resolves", ref assertions);
        Expect(EftItemTemplateIdResolver.Resolve(null) == string.Empty, "null shape is safely ignored", ref assertions);
        Expect(EftItemTemplateIdResolver.Resolve(new object()) == string.Empty, "unknown shape is safely ignored", ref assertions);

        List<EftItemViewHoverIntegration.HoverPatchTarget> targets = EftItemViewHoverIntegration.DiscoverTargets(new[]
        {
            typeof(FakeInventoryItemView).Assembly
        });
        bool fakeFound = false;
        for (int i = 0; i < targets.Count; i++)
            if (targets[i].Type == typeof(FakeInventoryItemView)) fakeFound = true;
        Expect(fakeFound, "pointer enter/exit ItemView pair is discovered", ref assertions);

        ItemPresentationStore store = new ItemPresentationStore();
        store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("abc", traderUnitValue: 12000, fleaUnitValue: 24000)
        }));
        RecordingSink sink = new RecordingSink();
        RecordingAnchorSink anchorSink = new RecordingAnchorSink();
        ItemHoverRuntimeController controller = new ItemHoverRuntimeController(store, sink);
        List<string> warnings = new List<string>();
        EftItemViewHoverIntegration integration = new EftItemViewHoverIntegration(controller, null, warnings.Add, anchorSink);

        DirectItemView hoveredView = new DirectItemView { Item = new DirectItem { TemplateId = "ABC" } };
        Expect(integration.DispatchEnter(hoveredView), "resolved enter is dispatched", ref assertions);
        Expect(sink.ShowCount == 1 && sink.Last.Primary == "24,000 ₽", "dispatch reaches cached hover pipeline and sink", ref assertions);
        Expect(object.ReferenceEquals(anchorSink.Last, hoveredView), "resolved enter binds the marker to the active ItemView", ref assertions);
        integration.DispatchExit();
        Expect(sink.ClearCount == 1 && !controller.HasActiveItem, "exit clears sink and active item", ref assertions);
        Expect(anchorSink.ClearCount == 1 && anchorSink.Last == null, "exit clears the ItemView marker anchor", ref assertions);

        Expect(!integration.DispatchEnter(new object()), "unresolved enter is ignored", ref assertions);
        Expect(anchorSink.ClearCount == 2, "unresolved ItemView clears a stale marker anchor", ref assertions);
        Expect(warnings.Count == 1, "unknown ItemView shape warns only once", ref assertions);
        integration.DispatchEnter(new object());
        Expect(warnings.Count == 1, "repeated unknown shape does not spam logs", ref assertions);

        Expect(integration.DispatchEnter(hoveredView) && object.ReferenceEquals(anchorSink.Last, hoveredView), "marker anchor can be rebound before disposal", ref assertions);
        integration.Dispose();
        Expect(anchorSink.Last == null, "dispose clears the marker anchor", ref assertions);
        Expect(!integration.DispatchEnter(new DirectItemView { Item = new DirectItem { TemplateId = "abc" } }), "disposed bridge rejects callbacks", ref assertions);
        Expect(!integration.IsInstalled && integration.PatchedMethodCount == 0, "dispose leaves bridge safely disabled", ref assertions);

        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 12 assertion failed: " + message);
    }

    sealed class DirectItemView { public DirectItem Item { get; set; } }
    sealed class DirectItem { public string TemplateId { get; set; } }
    sealed class ObfuscatedItemView { public ObfuscatedItem _item; }
    sealed class ObfuscatedItem { public string _tpl; }
    sealed class NestedItemView { public NestedItem Item; }
    sealed class NestedItem { public NestedTemplate Template; }
    sealed class NestedTemplate { public string _id; }

    sealed class FakeInventoryItemView
    {
        public DirectItem Item { get; set; }
        public void OnPointerEnter(object eventData) { }
        public void OnPointerExit(object eventData) { }
    }

    sealed class RecordingSink : IItemHoverViewSink
    {
        public int ShowCount;
        public int ClearCount;
        public ItemHoverText Last = ItemHoverText.Empty;
        public void Show(ItemHoverText text) { ShowCount++; Last = text; }
        public void Clear() { ClearCount++; Last = ItemHoverText.Empty; }
    }

    sealed class RecordingAnchorSink : IItemHoverAnchorSink
    {
        public object Last;
        public int ClearCount;
        public void SetAnchor(object itemView) { Last = itemView; }
        public void ClearAnchor() { ClearCount++; Last = null; }
    }
}
