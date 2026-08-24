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
        Expect(vendor.ValueLine == "Value: 118,230 ₽ · Therapist", "selected vendor value remains the primary line", ref assertions);
        Expect(Contains(vendor, ItemTooltipMode.Full, "Flea: 142,479 ₽"), "Full includes the alternate flea value", ref assertions);
        Expect(Contains(vendor, ItemTooltipMode.Full, "Per slot: 35,619 ₽"), "Full exposes cached best-value per-slot intelligence", ref assertions);
        Expect(!Contains(vendor, ItemTooltipMode.Detailed, "Flea: 142,479 ₽") && !Contains(vendor, ItemTooltipMode.Detailed, "Per slot: 35,619 ₽"),
            "Detailed remains compact and does not inherit Full valuation rows", ref assertions);

        ItemHoverText flea = new ItemHoverTextFormatter().Format(hover, ItemValueMode.Flea);
        Expect(flea.ValueLine == "Value: 142,479 ₽ · Flea", "selected flea value becomes the primary line", ref assertions);
        Expect(Contains(flea, ItemTooltipMode.Full, "Therapist: 118,230 ₽"), "Full preserves named best trader as the alternate source", ref assertions);

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
