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
        return assertions;
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
