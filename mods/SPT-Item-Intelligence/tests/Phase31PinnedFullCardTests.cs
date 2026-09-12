using System;
using System.IO;
using SPTItemIntelligence;

static class Phase31PinnedFullCardTests
{
    public static int Run()
    {
        int assertions = 0;
        ItemHoverText notNeeded = new ItemHoverText("", "", "", "known", 3, 0, 0, 0, 0,
            allocation: new ItemRequirementAllocation(3, 1, 0, 0, 0, 0, 0));
        ItemHoverText missing = new ItemHoverText("ITEM INTELLIGENCE", "", "No data for this item",
            "", 0, 0, 0, 0, 0, dataState: ItemDataState.Missing);
        ItemHoverText unavailable = new ItemHoverText("ITEM INTELLIGENCE", "", "Data unavailable",
            "", 0, 0, 0, 0, 0, dataState: ItemDataState.Unavailable);

        Expect(notNeeded.SummaryLine == "Not Needed" && !notNeeded.IsDiagnostic,
            "known irrelevant items have the authoritative Not Needed state", ref assertions);
        Expect(missing.IsDiagnostic && missing.DataState == ItemDataState.Missing && missing.Status != notNeeded.SummaryLine,
            "unknown item data cannot be mistaken for Not Needed", ref assertions);
        Expect(unavailable.IsDiagnostic && unavailable.DataState == ItemDataState.Unavailable,
            "runtime failure remains distinct from an unknown item", ref assertions);

        try
        {
            GameUiText.SetRussian(true);
            ItemHoverText russian = new ItemHoverText("ITEM INTELLIGENCE", "", "Данные недоступны",
                "", 0, 0, 0, 0, 0, dataState: ItemDataState.Unavailable);
            Expect(russian.GetLine(ItemTooltipMode.Normal, 1) == "Данные недоступны",
                "diagnostic presentation follows the game language", ref assertions);
        }
        finally { GameUiText.SetRussian(false); }

        string root = FindRepositoryRoot();
        string sink = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "ItemHoverOverlaySink.cs"));
        Expect(sink.Contains("pinned ? ItemTooltipMode.Full") && sink.Contains("pinnedView"),
            "marker click pins a Full card without changing the F12 mode", ref assertions);
        Expect(sink.Contains("guiEvent.type == EventType.MouseDown") && sink.Contains("guiEvent.Use()"),
            "the marker handles a deliberate left click", ref assertions);
        Expect(sink.Contains("!pinnedCardRect.Contains(mouse)") && sink.Contains("ClearPinned()"),
            "a click outside the pinned card closes it", ref assertions);
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
        if (!condition) throw new InvalidOperationException("Phase 31 assertion failed: " + message);
    }
}
