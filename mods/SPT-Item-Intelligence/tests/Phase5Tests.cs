using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase5Tests
{
    public static int Run()
    {
        int assertions = 0;

        RequirementProjection projection = new RequirementProjection(
            123,
            new[]
            {
                new OwnedTemplateCount("quest-item", 5),
                new OwnedTemplateCount("hideout-item", 1),
                new OwnedTemplateCount("unused-item", 3)
            },
            new[]
            {
                new RequirementContribution("quest-item", RequirementSource.CurrentQuest, 2, foundInRaidRequired: true),
                new RequirementContribution("quest-item", RequirementSource.FutureQuest, 1),
                new RequirementContribution("hideout-item", RequirementSource.Hideout, 2)
            });

        RequirementIndex requirements = RequirementIndexBuilder.Build(projection);
        ItemRequirementStateIndex states = ItemRequirementStateBuilder.Build(requirements);

        ItemRequirementState quest = states.Get("QUEST-ITEM");
        Expect(quest.OwnedCount == 5, "owned count", ref assertions);
        Expect(quest.QuestNeededNow == 2 && quest.QuestNeededLater == 1, "quest counts", ref assertions);
        Expect(quest.KeepCount == 3 && quest.SurplusCount == 2, "quest keep/surplus", ref assertions);
        Expect(quest.Decision == ItemRequirementDecision.SafeToSell && quest.IsSafeToSell, "surplus is safe to sell", ref assertions);
        Expect(quest.RequiresFoundInRaid, "FIR flag survives projection", ref assertions);
        Expect(quest.HoldReason == "Current quest (FIR)", "priority hold reason", ref assertions);

        ItemRequirementState hideout = states.Get("hideout-item");
        Expect(hideout.Decision == ItemRequirementDecision.Keep, "insufficient hideout stock is keep", ref assertions);
        Expect(hideout.KeepCount == 2 && hideout.SurplusCount == 0, "hideout keep count", ref assertions);
        Expect(hideout.HoldReason == "Hideout", "hideout hold reason", ref assertions);

        ItemRequirementState unused = states.Get("unused-item");
        Expect(unused.Decision == ItemRequirementDecision.SafeToSell, "owned item without requirement is sellable surplus", ref assertions);
        Expect(unused.KeepCount == 0 && unused.SurplusCount == 3, "unrequired surplus count", ref assertions);

        ItemRequirementState missing = states.Get("does-not-exist");
        Expect(object.ReferenceEquals(missing, ItemRequirementState.Empty), "missing lookup reuses singleton", ref assertions);

        ItemRequirementStateStore store = new ItemRequirementStateStore();
        store.Refresh(requirements);
        ItemRequirementStateIndex first = store.Current;
        Expect(first.GeneratedAtUnixSeconds == 123, "store publishes generation", ref assertions);
        store.Refresh(RequirementIndex.Empty);
        Expect(store.Current.Count == 0 && !object.ReferenceEquals(first, store.Current), "store atomically replaces snapshot", ref assertions);

        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 5 assertion failed: " + message);
    }
}
