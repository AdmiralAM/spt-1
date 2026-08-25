using System;
using System.IO;
using SPTItemIntelligence;

static class Phase29QuickSellReferenceTests
{
    public static int Run()
    {
        int assertions = 0;

        ItemPriceIndex index = ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("CACHE-TEMPLATE", 100000, "Mechanic", 125000, 0, 2, 1)
        });
        Expect(index.TryGet("cache-template", out ItemPriceState first), "template-keyed price index resolves normalized template id", ref assertions);
        Expect(index.TryGet("CACHE-TEMPLATE", out ItemPriceState second) && object.ReferenceEquals(first, second),
            "repeat pricing reads reuse the prepared immutable template state", ref assertions);
        Expect(first.BestSource == PriceSource.Flea && first.BestUnitValue == 125000 && first.ValuePerSlot == 62500,
            "best sell source and value-per-slot are precomputed before hover rendering", ref assertions);

        ItemPresentationStore store = new ItemPresentationStore();
        store.Refresh(ItemRequirementStateIndex.Empty, index);
        ItemHoverText text = new ItemHoverTextFormatter().Format(new ItemHoverState(store.Get("cache-template")), ItemValueMode.Vendor);
        Expect(Contains(text, ItemTooltipMode.Full, "Best sell: Flea"), "Full mode answers where to sell", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Full, "Best trader: Mechanic · 100,000 ₽"), "Full mode exposes best trader and trader sell price", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Full, "Flea: 125,000 ₽"), "Full mode exposes flea price", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Full, "Per slot: 62,500 ₽"), "Full mode exposes prepared value per slot", ref assertions);

        string root = FindRepositoryRoot();
        string renderer = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "PolishedTooltipRenderer.cs"));
        string overlay = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "ItemHoverOverlaySink.cs"));
        string server = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "server", "ServerMod.cs"));

        Expect(renderer.Contains("displayLineCache") && renderer.Contains("priceRenderCache") && renderer.Contains("semanticRenderCache"),
            "steady-state tooltip string transformations are cached instead of rebuilt on every repaint", ref assertions);
        Expect(renderer.Contains("RenderCacheLimit = 1024") && renderer.Contains("GetCachedPriceLine") && renderer.Contains("GetCachedSemanticLine"),
            "renderer caches are explicitly bounded and used by the draw path", ref assertions);
        Expect(overlay.Contains("Event.current.type != EventType.Repaint") && overlay.Contains("if (activeView == null) return"),
            "overlay keeps cheap early returns ahead of tooltip rendering", ref assertions);
        Expect(server.Contains("BuildPrices") && server.Contains("BuildSnapshotAsync") && !server.Contains("Timer") && !server.Contains("Update()"),
            "server pricing remains snapshot-built with no raid polling loop", ref assertions);

        string hoverFormatting = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "HoverFormatting.cs"));
        Expect(!hoverFormatting.Contains("SellItem") && !hoverFormatting.Contains("QuickSell") && !hoverFormatting.Contains("KeyCode"),
            "Item Intelligence remains decision support and contains no transaction execution or selling hotkeys", ref assertions);

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
        if (!condition) throw new InvalidOperationException("Phase 29 assertion failed: " + message);
    }
}
