using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase9HoverFormattingTests
{
    public static int Run()
    {
        int assertions = 0;
        ItemRequirementState requirement = new ItemRequirementState(
            "item-a", 5, 1, 2, 1, 4, 1,
            RequirementReasonFlags.CurrentQuest | RequirementReasonFlags.Hideout,
            ItemRequirementDecision.SafeToSell,
            "Current quest");
        ItemRequirementStateIndex requirements = new ItemRequirementStateIndex(1,
            new Dictionary<string, ItemRequirementState>(StringComparer.Ordinal) { ["item-a"] = requirement });
        ItemPriceIndex prices = ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("item-a", traderUnitValue: 22000, traderName: "Therapist", fleaUnitValue: 32000, width: 2, height: 1)
        });

        ItemPresentationStore store = new ItemPresentationStore();
        store.Refresh(requirements, prices);
        ItemHoverPresentationAdapter adapter = new ItemHoverPresentationAdapter(store);
        ItemHoverTextCache cache = new ItemHoverTextCache();
        ItemHoverState hover = adapter.OnHoverEnter("item-a");
        ItemHoverText first = cache.Get(hover, store.Current);

        Expect(first.Primary == "22,000 ₽ · Therapist", "vendor mode shows highest trader value and trader name", ref assertions);
        Expect(first.Secondary == "Flea: 32,000 ₽", "vendor mode preformats flea as the alternate Full value", ref assertions);
        Expect(!Contains(first, ItemTooltipMode.Detailed, "Flea: 32,000 ₽"), "alternate value stays out of Detailed mode", ref assertions);
        Expect(Contains(first, ItemTooltipMode.Full, "Flea: 32,000 ₽"), "Full mode exposes flea alongside selected trader value", ref assertions);

        ItemHoverText flea = new ItemHoverTextFormatter().Format(hover, ItemValueMode.Flea);
        Expect(flea.Primary == "32,000 ₽ · Flea", "flea mode shows flea value", ref assertions);
        Expect(flea.Secondary == "Therapist: 22,000 ₽", "flea mode preformats best trader as the alternate Full value", ref assertions);
        Expect(!Contains(flea, ItemTooltipMode.Normal, "Therapist: 22,000 ₽"), "alternate trader value stays Full-only", ref assertions);
        Expect(Contains(flea, ItemTooltipMode.Full, "Therapist: 22,000 ₽"), "Full mode exposes best trader alongside selected flea value", ref assertions);

        Expect(first.Status.Length == 0, "sell and surplus decisions stay out of user-facing hover text", ref assertions);
        Expect(object.ReferenceEquals(first, cache.Get(hover, store.Current)), "unchanged snapshot reuses formatted text object", ref assertions);
        Expect(object.ReferenceEquals(ItemHoverText.Empty, cache.Get(ItemHoverState.Empty, store.Current)), "empty hover is allocation-free", ref assertions);

        store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndex.Empty);
        Expect(object.ReferenceEquals(ItemHoverText.Empty, cache.Get(adapter.OnHoverEnter("item-a"), store.Current)), "new snapshot invalidates old formatted entry", ref assertions);
        return assertions;
    }

    static bool Contains(ItemHoverText text, ItemTooltipMode mode, string expected)
    {
        for (int i = 0; i < text.GetLineCount(mode); i++)
            if (text.GetLine(mode, i) == expected) return true;
        return false;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 9 assertion failed: " + message);
    }
}
