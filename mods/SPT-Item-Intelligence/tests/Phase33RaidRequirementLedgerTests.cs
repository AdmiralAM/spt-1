using System;
using SPTItemIntelligence;

static class Phase33RaidRequirementLedgerTests
{
    public static int Run()
    {
        int assertions = 0;
        RaidRequirementLedger ledger = new RaidRequirementLedger();
        ItemRequirementAllocation demand = new ItemRequirementAllocation(0, 0, 1, 0, 2, 1, 0);

        Expect(ledger.Observe("loot-a", "TPL", 1, true), "first pickup changes the ledger", ref assertions);
        Expect(!ledger.Observe("loot-a", "tpl", 1, true), "repeated observation of one instance is idempotent", ref assertions);
        Expect(ledger.Get("tpl").Owned == 1 && ledger.Get("tpl").FoundInRaid == 1,
            "one physical instance is counted once", ref assertions);
        Expect(ledger.Evaluate("tpl", demand, true).PrimaryReason == ItemNeedReason.Hideout,
            "FIR pickup fills the FIR-only active quest before hideout", ref assertions);

        Expect(ledger.Observe("loot-b", "tpl", 2, true), "a stack pickup changes the ledger", ref assertions);
        Expect(ledger.Get("tpl").Owned == 3 && ledger.Get("tpl").FoundInRaid == 3,
            "stack size contributes exact units", ref assertions);
        Expect(!SenseRequirementMapper.Map(ledger.Evaluate("tpl", demand, true)).OverridesSense,
            "the final required stack produces Enough", ref assertions);

        Expect(ledger.Observe("loot-b", "tpl", 1, true), "stack shrink is an event update", ref assertions);
        Expect(ledger.Get("tpl").Owned == 2 && ledger.Evaluate("tpl", demand, true).Remaining == 1,
            "stack shrink restores the exact residual", ref assertions);
        Expect(ledger.Remove("loot-a"), "drop removes the observed instance", ref assertions);
        Expect(ledger.Get("tpl").Owned == 1 && ledger.Evaluate("tpl", demand, true).PrimaryReason == ItemNeedReason.Hideout,
            "drop returns the requirement to the correct allocation state", ref assertions);
        Expect(!ledger.Remove("loot-a"), "duplicate removal is idempotent", ref assertions);

        ledger.Reset();
        Expect(ledger.ItemCount == 0 && ledger.Get("tpl").Owned == 0,
            "raid reset clears all local reservations", ref assertions);
        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 33 assertion failed: " + message);
    }
}
