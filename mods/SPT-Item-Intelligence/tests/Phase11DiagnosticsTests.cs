using System;
using SPTItemIntelligence;

static class Phase11DiagnosticsTests
{
    public static int Run()
    {
        int assertions = 0;

        ItemRequirementState requirement = new ItemRequirementState(
            "diag-item",
            ownedCount: 4,
            questNeededNow: 1,
            questNeededLater: 2,
            hideoutNeeded: 1,
            keepCount: 3,
            surplusCount: 1,
            reasons: RequirementReasonFlags.CurrentQuest | RequirementReasonFlags.FoundInRaid | RequirementReasonFlags.Hideout,
            decision: ItemRequirementDecision.SafeToSell,
            holdReason: "Current Quest (FIR)");

        ItemPriceState price = ItemPriceEvaluator.Evaluate(new ItemPriceInput(
            "diag-item",
            traderUnitValue: 12000,
            traderName: "Therapist",
            fleaUnitValue: 25000,
            fallbackUnitValue: 1000,
            width: 2,
            height: 1,
            stackCount: 2));

        ItemPresentationState state = new ItemPresentationState("diag-item", requirement, price);
        ItemDecisionDiagnostic diagnostic = ItemDecisionDiagnostics.Capture(state);

        Expect(diagnostic.HasData, "diagnostic has data", ref assertions);
        Expect(diagnostic.TemplateId == "diag-item", "template id preserved", ref assertions);
        Expect(diagnostic.BestPriceSource == PriceSource.Flea, "best price source preserved", ref assertions);
        Expect(diagnostic.TotalValue == 50000 && diagnostic.ValuePerSlot == 25000, "price values preserved", ref assertions);
        Expect(diagnostic.Decision == ItemRequirementDecision.SafeToSell, "decision preserved", ref assertions);
        Expect(diagnostic.OwnedCount == 4 && diagnostic.KeepCount == 3 && diagnostic.SurplusCount == 1, "inventory counts preserved", ref assertions);
        Expect(diagnostic.QuestNeededNow == 1 && diagnostic.QuestNeededLater == 2 && diagnostic.HideoutNeeded == 1, "requirement counts preserved", ref assertions);
        Expect(diagnostic.RequiresFoundInRaid, "FIR requirement preserved", ref assertions);
        Expect(diagnostic.HoldReason == "Current Quest (FIR)", "hold reason preserved", ref assertions);
        Expect(diagnostic.HasPriceData && diagnostic.HasRequirementData, "data flags preserved", ref assertions);

        ItemDecisionDiagnostic empty = ItemDecisionDiagnostics.Capture(ItemPresentationState.Empty);
        Expect(object.ReferenceEquals(empty, ItemDecisionDiagnostic.Empty), "empty diagnostic is canonical", ref assertions);
        Expect(!empty.HasData && !empty.HasPriceData && !empty.HasRequirementData, "empty diagnostic has no data", ref assertions);

        ItemPresentationStore store = new ItemPresentationStore();
        ItemDecisionDiagnostic missing = ItemDecisionDiagnostics.Capture(store, "missing");
        Expect(object.ReferenceEquals(missing, ItemDecisionDiagnostic.Empty), "missing store entry uses canonical empty diagnostic", ref assertions);

        bool threw = false;
        try { ItemDecisionDiagnostics.Capture((ItemPresentationStore)null, "x"); }
        catch (ArgumentNullException) { threw = true; }
        Expect(threw, "null store rejected", ref assertions);

        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 11 assertion failed: " + message);
    }
}
