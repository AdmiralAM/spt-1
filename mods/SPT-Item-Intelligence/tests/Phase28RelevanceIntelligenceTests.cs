using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase28RelevanceIntelligenceTests
{
    public static int Run()
    {
        int assertions = 0;
        Dictionary<string, object> profile = new Dictionary<string, object>
        {
            ["Inventory"] = new Dictionary<string, object>
            {
                ["equipment"] = "equip-root",
                ["items"] = new object[]
                {
                    Item("eq-a", "REL", "equip-root", 2),
                    Item("eq-child", "REL", "eq-a", 3),
                    Item("stash-a", "REL", "stash-root", 7)
                }
            }
        };
        object[] prices =
        {
            new Dictionary<string, object>
            {
                ["templateId"] = "REL",
                ["craftCount"] = 2,
                ["barterCount"] = 4
            }
        };
        RequirementDataEnvelope envelope = new RequirementDataEnvelope(1, profile, new object[0], new Dictionary<string, object>(), prices);
        RelevanceSnapshotDecoder decoder = new RelevanceSnapshotDecoder(new StubDecoder(envelope));
        decoder.Decode("ignored");

        ItemRelevanceState relevance = ItemRelevanceRegistry.Get("rel");
        Expect(relevance.CraftCount == 2, "craft count is projected from the existing snapshot", ref assertions);
        Expect(relevance.BarterCount == 4, "barter count is projected from the existing snapshot", ref assertions);
        Expect(relevance.OnYouCount == 5, "on-you count includes equipment descendants and stack counts but excludes stash", ref assertions);

        ItemHoverText text = new ItemHoverText("10,000 ₽ · Therapist", "Flea: 12,000 ₽", "", "rel", 12, 0, 0, 0, 0, perSlotLine: "Per slot: 12,000 ₽");
        Expect(Contains(text, ItemTooltipMode.Full, "Craft ×2"), "Full exposes compact craft relevance", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Full, "Barter ×4"), "Full exposes compact barter relevance", ref assertions);
        Expect(ContainsFragment(text, ItemTooltipMode.Full, "On You ×5"), "Full owned row exposes cached on-you count", ref assertions);
        Expect(!Contains(text, ItemTooltipMode.Normal, "Craft ×2") && !Contains(text, ItemTooltipMode.Detailed, "Barter ×4"),
            "craft and barter relevance stay Full-only", ref assertions);

        ItemRelevanceRegistry.Replace(null);
        return assertions;
    }

    static Dictionary<string, object> Item(string id, string tpl, string parentId, int count)
    {
        return new Dictionary<string, object>
        {
            ["_id"] = id,
            ["_tpl"] = tpl,
            ["parentId"] = parentId,
            ["upd"] = new Dictionary<string, object> { ["StackObjectsCount"] = count }
        };
    }

    static bool Contains(ItemHoverText text, ItemTooltipMode mode, string expected)
    {
        for (int i = 0; i < text.GetLineCount(mode); i++)
            if (text.GetLine(mode, i) == expected) return true;
        return false;
    }

    static bool ContainsFragment(ItemHoverText text, ItemTooltipMode mode, string expected)
    {
        for (int i = 0; i < text.GetLineCount(mode); i++)
            if (text.GetLine(mode, i).IndexOf(expected, StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 28 assertion failed: " + message);
    }

    sealed class StubDecoder : IRequirementSnapshotDecoder
    {
        readonly RequirementDataEnvelope envelope;
        public StubDecoder(RequirementDataEnvelope envelope) { this.envelope = envelope; }
        public RequirementDataEnvelope Decode(string json) { return envelope; }
    }
}
