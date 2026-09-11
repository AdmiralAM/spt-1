using System;
using System.IO;
using SPTItemIntelligence;

static class Phase30LocalizedCardInteractionTests
{
    public static int Run()
    {
        int assertions = 0;
        try
        {
            GameUiText.SetRussian(true);
            ItemRequirementAllocation allocation = new ItemRequirementAllocation(2, 1, 2, 3, 1, 2, 2);
            ItemHoverText russian = new ItemHoverText("10,000 ₽", "", "", "tpl", 2, 2, 3, 1, 6,
                ownedFoundInRaid: 1, questNowFoundInRaid: 2, questLaterFoundInRaid: 2, allocation: allocation);
            Expect(russian.SummaryLine == "Нужно ещё ×4 · Оставить ×6", "Russian summary follows the game-language selection and reports total keep demand", ref assertions);
            Expect(russian.SummaryOwnedLine == "В наличии ×2 · Найдено в рейде ×1", "Russian owned/FIR semantics stay explicit", ref assertions);
            Expect(russian.QuestNowLine.StartsWith("Активный квест:", StringComparison.Ordinal), "Russian active quest label is distinct", ref assertions);
            Expect(russian.QuestLaterLine.StartsWith("Будущий квест:", StringComparison.Ordinal), "Russian future quest label is distinct", ref assertions);
            Expect(russian.HideoutLine.StartsWith("Убежище:", StringComparison.Ordinal), "Russian hideout label is distinct", ref assertions);
        }
        finally { GameUiText.SetRussian(false); }

        string root = FindRepositoryRoot();
        string sink = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "ItemHoverOverlaySink.cs"));
        string settings = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "UiSettings.cs"));
        Expect(sink.Contains("Marker.TryGetScreenRect(out result)"), "marker is the primary tooltip hotspot", ref assertions);
        Expect(sink.Contains("cell.height * 0.24f") && sink.Contains("new Rect(cell.xMin, cell.yMin, cell.width, height)"),
            "markerless items use only the native caption strip as fallback hotspot", ref assertions);
        Expect(!sink.Contains("if (!markerRect.Contains(mouse))") || sink.Contains("TryGetTooltipHotspot"),
            "whole-cell hover no longer drives the information card", ref assertions);
        Expect(sink.Contains("color.a = 1f") && settings.Contains("\"Background Coloring (Valuation)\", true"),
            "the accepted valuation palette is enabled by default without alpha-shifting its colors", ref assertions);
        Expect(BackgroundPalette.Ammo(20) == "#526B3F" && BackgroundPalette.Ammo(21) == "#253552" && BackgroundPalette.Ammo(71) == "#5C4825",
            "ammunition retains the accepted penetration-specific thresholds and colors", ref assertions);
        Expect(sink.Contains("anchor.GetComponentsInChildren(imageType, true)") && sink.Contains("originalColor") &&
               !sink.Contains("new GameObject(\"ItemIntelligenceBackground\""),
            "valuation color uses and restores the native cell background instead of blending a second rectangle", ref assertions);

        ItemHoverText modes = new ItemHoverText("10,000 ₽ · Therapist", "Flea: 20,000 ₽", "", "mode", 1, 1, 1, 1, 3,
            requirementDetails: new[] { "Now: Quest A ×1", "Later: Quest B ×1", "Hideout: Station L2 ×1" },
            perSlotLine: "Per slot: 5,000 ₽", bestTraderLine: "Trader: Therapist · 10,000 ₽", fleaPriceLine: "Flea: 20,000 ₽");
        Expect(Contains(modes, ItemTooltipMode.Normal, "Value: 10,000 ₽ · Therapist") && !Contains(modes, ItemTooltipMode.Normal, "Per slot: 5,000 ₽"),
            "Normal shows only the selected price source and omits per-slot value", ref assertions);
        Expect(Contains(modes, ItemTooltipMode.Detailed, "Now: Quest A ×1") && !Contains(modes, ItemTooltipMode.Detailed, "Hideout: Station L2 ×1"),
            "Detailed adds exactly the nearest concrete target", ref assertions);
        Expect(Contains(modes, ItemTooltipMode.Full, "Trader: Therapist · 10,000 ₽") && Contains(modes, ItemTooltipMode.Full, "Flea: 20,000 ₽") && Contains(modes, ItemTooltipMode.Full, "Per slot: 5,000 ₽"),
            "Full exposes both price sources and per-slot value", ref assertions);
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
        if (!condition) throw new InvalidOperationException("Phase 30 assertion failed: " + message);
    }
}
