using System;
using System.IO;
using SPTItemIntelligence;

static class Phase27ValueIntelligenceTests
{
    public static int Run()
    {
        int assertions = 0;

        ItemPresentationStore store = new ItemPresentationStore();
        store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("VALUE-RD", 118230, "Therapist", 142479, 9000, 2, 2)
        }));

        ItemHoverState hover = new ItemHoverState(store.Get("value-rd"));
        ItemHoverText vendor = new ItemHoverTextFormatter().Format(hover, ItemValueMode.Vendor);
        Expect(vendor.ValueLine == "Value: 118,230 ₽ · Therapist", "selected vendor value remains the compact-mode primary line", ref assertions);
        Expect(Contains(vendor, ItemTooltipMode.Full, "Best sell: Flea"), "Full exposes the best sell destination from prepared price state", ref assertions);
        Expect(Contains(vendor, ItemTooltipMode.Full, "Best trader: Therapist · 118,230 ₽"), "Full exposes named best trader and sell price", ref assertions);
        Expect(Contains(vendor, ItemTooltipMode.Full, "Flea: 142,479 ₽"), "Full exposes flea value", ref assertions);
        Expect(Contains(vendor, ItemTooltipMode.Full, "Per slot: 35,619 ₽"), "Full exposes cached best-value per-slot intelligence", ref assertions);
        Expect(!Contains(vendor, ItemTooltipMode.Full, "Value: 118,230 ₽ · Therapist"), "Full avoids duplicating the compact selected-source Value row", ref assertions);
        Expect(!Contains(vendor, ItemTooltipMode.Detailed, "Flea: 142,479 ₽") && !Contains(vendor, ItemTooltipMode.Detailed, "Per slot: 35,619 ₽"),
            "Detailed remains compact and does not inherit Full valuation rows", ref assertions);

        ItemHoverText flea = new ItemHoverTextFormatter().Format(hover, ItemValueMode.Flea);
        Expect(flea.ValueLine == "Value: 142,479 ₽ · Flea", "selected flea value remains the compact-mode primary line", ref assertions);
        Expect(Contains(flea, ItemTooltipMode.Full, "Best sell: Flea"), "Full sell destination is independent from preferred compact value mode", ref assertions);
        Expect(Contains(flea, ItemTooltipMode.Full, "Best trader: Therapist · 118,230 ₽"), "Full preserves named best trader regardless of preferred compact source", ref assertions);

        ItemPresentationStore fallbackStore = new ItemPresentationStore();
        fallbackStore.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("FLEA-ONLY", 0, "", 75000, 0),
            new ItemPriceInput("TRADER-ONLY", 64000, "Mechanic", 0, 0)
        }));
        ItemHoverText vendorFallback = new ItemHoverTextFormatter().Format(
            new ItemHoverState(fallbackStore.Get("flea-only")), ItemValueMode.Vendor);
        Expect(vendorFallback.ValueLine == "Value: 75,000 ₽ · Flea" && vendorFallback.Secondary.Length == 0,
            "unavailable selected vendor source falls back to flea instead of rendering an empty value tooltip", ref assertions);
        Expect(Contains(vendorFallback, ItemTooltipMode.Full, "Best sell: Flea") && Contains(vendorFallback, ItemTooltipMode.Full, "Flea: 75,000 ₽"),
            "flea-only items still expose an explicit sell destination and price", ref assertions);
        ItemHoverText fleaFallback = new ItemHoverTextFormatter().Format(
            new ItemHoverState(fallbackStore.Get("trader-only")), ItemValueMode.Flea);
        Expect(fleaFallback.ValueLine == "Value: 64,000 ₽ · Mechanic" && fleaFallback.Secondary.Length == 0,
            "unavailable selected flea source falls back to the named trader", ref assertions);
        Expect(Contains(fleaFallback, ItemTooltipMode.Full, "Best sell: Mechanic") && Contains(fleaFallback, ItemTooltipMode.Full, "Best trader: Mechanic · 64,000 ₽"),
            "trader-only items still expose explicit best trader destination and price", ref assertions);

        string root = FindRepositoryRoot();
        string renderer = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "PolishedTooltipRenderer.cs"));
        Expect(renderer.Contains("const long PriceGreen = 50000") && renderer.Contains("const long PriceRed = 100000") && renderer.Contains("const long PriceGold = 250000"),
            "price bands use the accepted 50k/100k/250k thresholds", ref assertions);
        Expect(renderer.Contains("ApplyPriceAmountColor") && renderer.Contains("TryReadRoubleAmount"),
            "price coloring is presentation-only and parses already formatted cached value rows", ref assertions);
        Expect(renderer.Contains("return Color.white") && renderer.Contains("0.38f, 0.90f, 0.42f") && renderer.Contains("1f, 0.32f, 0.28f") && renderer.Contains("1f, 0.72f, 0.18f"),
            "price bands resolve to white, green, red, and gold without changing marker colors", ref assertions);
        return assertions;
    }

    static bool Contains(ItemHoverText text, ItemTooltipMode mode, string expected)
    {
        for (int i = 0; i < text.GetLineCount(mode); i++)
            if (text.GetLine(mode, i) == expected) return true;
        return false;
    }

    static string FindRepositoryRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "mods", "SPT-Item-Intelligence"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 27 assertion failed: " + message);
    }
}
