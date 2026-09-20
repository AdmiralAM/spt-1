using System;
using System.IO;
using SPTItemIntelligence;

static class Phase36RaidInventoryTooltipTests
{
    public static int Run()
    {
        int assertions = 0;
        ItemRequirementAllocation allocation = new ItemRequirementAllocation(2, 1, 3, 0, 1, 2, 0);
        ItemRequirementState requirement = new ItemRequirementState(
            "tpl", 2, 3, 0, 1, 4, 0,
            RequirementReasonFlags.CurrentQuest | RequirementReasonFlags.Hideout | RequirementReasonFlags.FoundInRaid,
            ItemRequirementDecision.Keep, "Current quest (FIR)", null, allocation);
        ItemPresentationState baseline = new ItemPresentationState("tpl", requirement, null);
        RaidRequirementLedger ledger = new RaidRequirementLedger();
        ledger.BeginRaid();
        ledger.Observe("raid-stack", "tpl", 3, true);

        ItemPresentationState combined = ledger.Apply(baseline);
        Expect(combined.Requirement.OwnedCount == 5 && combined.Requirement.Allocation.OwnedFir == 4,
            "raid stack is included in the authoritative owned/FIR totals", ref assertions);
        Expect(combined.Requirement.Allocation.Coverage == RequirementCoverage.Enough,
            "combined stash and raid stock drives the final state", ref assertions);

        ItemHoverText text = new ItemHoverTextFormatter().Format(new ItemHoverState(combined));
        Expect(text.SummaryOwnedLine == "In raid ×3", "regular modes expose only current-raid stock", ref assertions);
        Expect(text.TotalOwnedLine == "Total (stash + raid) ×5", "Full preserves the combined stash and raid total", ref assertions);
        Expect(text.OwnedBreakdownLine == "FIR ×4 · non-FIR ×1", "Full owns the FIR/non-FIR breakdown", ref assertions);
        Expect(text.RequirementBreakdownLine == "Required: total ×4 · quest FIR ×2", "Full states the exact item total and its FIR-only quest portion", ref assertions);
        Expect(text.RequirementSourcesLine == "Sources: quests ×3 · hideout ×1",
            "Full makes the quest and hideout contribution to the total explicit", ref assertions);
        ItemHoverText unrestricted = new ItemHoverText("", "", "", "tpl", 0, 0, 0, 9, 9);
        Expect(unrestricted.RequirementBreakdownLine == "Required: total ×9", "hideout-only requirements omit low-value FIR prose", ref assertions);
        ItemRequirementAllocation firHideout = new ItemRequirementAllocation(2, 1, 0, 0, 2, 0, 0, hideoutFir: 2);
        Expect(firHideout.HideoutFirRequired == 2 && firHideout.HideoutFirAllocated == 1 && firHideout.HideoutMissing == 1,
            "FIR-only hideout stock is reserved before unrestricted consumption", ref assertions);
        ItemHoverText hideoutProgress = new ItemHoverText("", "", "", "hideout", 0, 0, 0, 2, 2,
            allocation: new ItemRequirementAllocation(0, 0, 0, 0, 2, 0, 0, hideoutFir: 2,
                hideoutInstalled: 3, hideoutCurrentRequired: 5));
        Expect(hideoutProgress.HideoutLine == "For hideout after quests: 0/2" &&
               hideoutProgress.HideoutInstalledLine == "In hideout: 3/5",
            "Full replaces hideout FIR prose with current-stage installed progress", ref assertions);
        Expect(hideoutProgress.RequirementBreakdownLine == "Required: total ×2",
            "generic FIR wording describes quest obligations rather than hideout defaults", ref assertions);
        Expect(!Contains(text, ItemTooltipMode.Normal, "non-FIR"), "Normal omits the breakdown", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Full, "FIR ×4 · non-FIR ×1"), "Full renders the breakdown", ref assertions);
        Expect(!Contains(text, ItemTooltipMode.Normal, "Total (stash + raid) ×5") &&
               Contains(text, ItemTooltipMode.Full, "Total (stash + raid) ×5"),
            "combined stock is available only from Full", ref assertions);
        string sink = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "mods", "SPT-Item-Intelligence", "src", "ItemHoverOverlaySink.cs"));
        Expect(sink.Contains("if (pinnedView != null || Volatile.Read(ref hoveredView) != null)") &&
               sink.Contains("RaidInventoryRefreshRequested?.Invoke();"),
            "an inspected item refreshes raid inventory without reopening the inventory window", ref assertions);
        string plugin = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "mods", "SPT-Item-Intelligence", "src", "Plugin.cs"));
        Expect(plugin.Contains("WaitForSecondsRealtime(InventorySnapshotSettleSeconds)") &&
               plugin.Contains("InventorySnapshotMinimumSeconds = 1.5f") &&
               plugin.Contains("RaidInventoryMinimumScanSeconds = .2f") &&
               plugin.Contains("RaidInventoryPollSeconds = .2f") &&
               plugin.Contains("now - lastRaidInventoryScanAt < RaidInventoryMinimumScanSeconds"),
            "view bursts coalesce while the lightweight raid inventory scan stays responsive and rate limited", ref assertions);
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

    static bool Contains(ItemHoverText text, ItemTooltipMode mode, string expected)
    {
        for (int i = 0; i < text.GetLineCount(mode); i++)
            if (text.GetLine(mode, i).Contains(expected, StringComparison.Ordinal)) return true;
        return false;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 36 assertion failed: " + message);
    }
}
