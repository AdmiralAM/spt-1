using System;
using SPTItemIntelligence;

public static class Phase2Tests
{
    static int assertions;

    public static int Run()
    {
        SafeToSellEvaluator evaluator = new SafeToSellEvaluator();

        SafeToSellResult none = evaluator.Evaluate(Snapshot("  ABC  ", 3, 1));
        Expect(none.TemplateId == "abc", "template id normalization");
        Expect(none.Decision == ItemDecision.SafeToSell && none.SafeSurplus == 3, "unprotected inventory is surplus");
        Expect(none.Summary == "SAFE TO SELL: 3", "surplus summary");

        SafeToSellResult empty = evaluator.Evaluate(Snapshot("empty", 0, 0));
        Expect(empty.Decision == ItemDecision.NoRequirement, "empty item has no requirement");
        Expect(empty.Summary == "NO CURRENT/NEAR REQUIREMENT", "no requirement summary");

        SafeToSellResult quest = evaluator.Evaluate(Snapshot("salewa", 2, 2,
            Req(RequirementScope.ActiveQuest, "Therapist quest", 3, true)));
        Expect(quest.Decision == ItemDecision.Keep, "quest deficit keeps item");
        Expect(quest.ProtectedOwned == 2 && quest.MissingFoundInRaid == 1, "FIR protection and deficit");
        Expect(quest.Summary == "KEEP 2 — Therapist quest", "keep summary");

        SafeToSellResult firScarcity = evaluator.Evaluate(Snapshot("fir", 10, 1,
            Req(RequirementScope.ActiveQuest, "FIR quest", 4, true),
            Req(RequirementScope.ActiveQuest, "Flexible quest", 1)));
        Expect(firScarcity.ProtectedOwned == 2, "only eligible owned units are protected");
        Expect(firScarcity.MissingFoundInRaid == 3 && firScarcity.MissingFlexible == 0, "eligibility deficits remain separate");
        Expect(firScarcity.SafeSurplus == 8, "ineligible units are not falsely protected");

        SafeToSellResult mixed = evaluator.Evaluate(Snapshot("mixed", 5, 4,
            Req(RequirementScope.ActiveQuest, "FIR quest", 3, true),
            Req(RequirementScope.SelectedHideoutTarget, "Water Collector 3", 2)));
        Expect(mixed.ProtectedOwned == 5 && mixed.SafeSurplus == 0, "FIR reserve then flexible allocation");
        Expect(mixed.MissingTotal == 0, "fully covered requirements have no deficit");

        SafeToSellResult priority = evaluator.Evaluate(Snapshot("priority", 0, 0,
            Req(RequirementScope.Craft, "Craft", 1),
            Req(RequirementScope.Wishlist, "Wishlist", 1),
            Req(RequirementScope.NearFutureQuest, "Future quest", 1, false, 1),
            Req(RequirementScope.NextHideoutUpgrade, "Next hideout", 1),
            Req(RequirementScope.SelectedHideoutTarget, "Selected target", 1),
            Req(RequirementScope.ActiveQuest, "Active normal", 1),
            Req(RequirementScope.ActiveQuest, "Active FIR", 1, true)),
            new SafeToSellPolicy { IncludeCrafts = true });
        Expect(priority.HighestPriorityReason.Reason == "Active FIR", "active FIR is highest priority");
        Expect(priority.Allocations[1].Requirement.Reason == "Active normal", "active normal is second priority");
        Expect(priority.Allocations[2].Requirement.Reason == "Selected target", "selected hideout is third priority");
        Expect(priority.Allocations[3].Requirement.Reason == "Next hideout", "next hideout follows selected target");
        Expect(priority.Allocations[4].Requirement.Reason == "Future quest", "near quest precedes wishlist");
        Expect(priority.Allocations[5].Requirement.Reason == "Wishlist", "wishlist precedes craft");

        SafeToSellResult horizon = evaluator.Evaluate(Snapshot("horizon", 0, 0,
            Req(RequirementScope.NearFutureQuest, "Near", 2, false, 2),
            Req(RequirementScope.NearFutureQuest, "Far", 5, false, 3)));
        Expect(horizon.Allocations.Count == 1, "default progression horizon excludes distant quests");
        Expect(horizon.MissingFlexible == 2, "only near quest contributes deficit");

        SafeToSellResult noFuture = evaluator.Evaluate(Snapshot("no-future", 2, 0,
            Req(RequirementScope.NearFutureQuest, "Near", 2, false, 1)),
            new SafeToSellPolicy { IncludeNearFutureQuests = false });
        Expect(noFuture.SafeSurplus == 2 && noFuture.Allocations.Count == 0, "near-future scope can be disabled");

        SafeToSellResult optional = evaluator.Evaluate(Snapshot("optional", 4, 0,
            Req(RequirementScope.Barter, "Barter", 2),
            Req(RequirementScope.Craft, "Craft", 2)));
        Expect(optional.SafeSurplus == 4 && optional.Allocations.Count == 0, "barter and craft are optional by default");

        SafeToSellResult optionalOn = evaluator.Evaluate(Snapshot("optional-on", 4, 0,
            Req(RequirementScope.Barter, "Barter", 2),
            Req(RequirementScope.Craft, "Craft", 2)),
            new SafeToSellPolicy { IncludeBarters = true, IncludeCrafts = true });
        Expect(optionalOn.ProtectedOwned == 4 && optionalOn.SafeSurplus == 0, "optional scopes can be enabled");

        SafeToSellResult disabled = evaluator.Evaluate(Snapshot("disabled", 1, 0,
            new ItemRequirement(RequirementScope.ActiveQuest, "Disabled", 5, enabled: false),
            Req(RequirementScope.ActiveQuest, "Zero", 0)));
        Expect(disabled.SafeSurplus == 1 && disabled.Allocations.Count == 0, "disabled and zero requirements are ignored");

        ItemRequirementSnapshot clamped = Snapshot("clamped", -4, 9);
        Expect(clamped.OwnedTotal == 0 && clamped.OwnedFoundInRaid == 0, "owned counts are clamped");

        ItemRequirement normalized = new ItemRequirement(RequirementScope.Wishlist, "  Wishlist item  ", -2, prerequisiteDistance: -3);
        Expect(normalized.Reason == "Wishlist item" && normalized.RequiredCount == 0 && normalized.PrerequisiteDistance == 0, "requirement values are normalized");

        bool threw = false;
        try { evaluator.Evaluate(null); }
        catch (ArgumentNullException) { threw = true; }
        Expect(threw, "null snapshot rejected");

        return assertions;
    }

    static ItemRequirementSnapshot Snapshot(string id, int total, int fir, params ItemRequirement[] requirements)
    {
        return new ItemRequirementSnapshot(id, total, fir, requirements);
    }

    static ItemRequirement Req(RequirementScope scope, string reason, int count, bool fir = false, int distance = 0)
    {
        return new ItemRequirement(scope, reason, count, fir, distance);
    }

    static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
    }
}
