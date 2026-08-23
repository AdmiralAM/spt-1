using System;
using System.Linq;
using SPTBeltArmbandInventory;

internal static class Program
{
    static int assertions;
    static readonly string[] Vanilla =
    {
        BeltSlotPlan.TacticalVest,
        BeltSlotPlan.Pockets,
        BeltSlotPlan.Backpack,
        BeltSlotPlan.SecuredContainer,
        BeltSlotPlan.Dogtag
    };

    static void Main()
    {
        Assert(BeltSlotPlan.IsExpectedContainerPanelOrder(Vanilla), "recognizes SPT 4.1 container order");
        Assert(!BeltSlotPlan.IsExpectedContainerPanelOrder(new[] { BeltSlotPlan.Pockets }), "rejects unrelated enum arrays");

        string[] above = BeltSlotPlan.Build(Vanilla, BeltSlotPosition.AbovePockets, true);
        Assert(Array.IndexOf(above, BeltSlotPlan.ArmBand) + 1 == Array.IndexOf(above, BeltSlotPlan.Pockets), "places belt above pockets");
        Assert(above.Length == Vanilla.Length + 1, "adds exactly one belt row");

        string[] below = BeltSlotPlan.Build(Vanilla, BeltSlotPosition.BelowPockets, true);
        Assert(Array.IndexOf(below, BeltSlotPlan.ArmBand) == Array.IndexOf(below, BeltSlotPlan.Pockets) + 1, "places belt below pockets");
        Assert(below.Length == Vanilla.Length + 1, "below layout adds exactly one row");

        string[] duplicateInput = Vanilla.Concat(new[] { BeltSlotPlan.ArmBand, BeltSlotPlan.ArmBand }).ToArray();
        string[] normalized = BeltSlotPlan.Build(duplicateInput, BeltSlotPosition.BelowPockets, true);
        Assert(normalized.Count(x => x == BeltSlotPlan.ArmBand) == 1, "layout is idempotent");

        string[] hidden = BeltSlotPlan.Build(duplicateInput, BeltSlotPosition.AbovePockets, false);
        Assert(!hidden.Contains(BeltSlotPlan.ArmBand), "plain armband removes duplicate belt row");
        Assert(hidden.SequenceEqual(Vanilla), "hidden layout preserves vanilla order");

        Assert(!BeltSlotPlan.ShouldExposeBelt(false, false), "empty slot stays hidden");
        Assert(!BeltSlotPlan.ShouldExposeBelt(true, false), "plain armband stays hidden");
        Assert(!BeltSlotPlan.ShouldExposeBelt(false, true), "container flag alone is insufficient");
        Assert(BeltSlotPlan.ShouldExposeBelt(true, true), "equipped container exposes belt row");

        string[] unusual = { BeltSlotPlan.TacticalVest, BeltSlotPlan.Backpack, BeltSlotPlan.SecuredContainer, BeltSlotPlan.Dogtag };
        string[] fallback = BeltSlotPlan.Build(unusual, BeltSlotPosition.AbovePockets, true);
        Assert(fallback[fallback.Length - 1] == BeltSlotPlan.ArmBand, "missing pockets falls back safely");
        Assert(unusual.SequenceEqual(BeltSlotPlan.Build(unusual, BeltSlotPosition.BelowPockets, false)), "disabled fallback does not invent slots");

        Console.WriteLine("SPT Belt/Armband Inventory Phase 1: " + assertions + " assertions passed.");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
        assertions++;
    }
}
