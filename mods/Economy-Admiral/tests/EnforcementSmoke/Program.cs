using SPTEconomy;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException($"Economy Admiral Enforce smoke: {message}");
}

var xpA = 8000d;
var xpB = 2000d;
var success = NumericRewardTransactionCore.Execute([
    new NumericRewardTransactionRequest
    {
        QuestId = "mod-added-outlier",
        Dimension = "Experience",
        ExpectedBefore = 10000,
        Target = 3000,
        Slots = [
            new NumericRewardSlot(() => xpA, value => xpA = value),
            new NumericRewardSlot(() => xpB, value => xpB = value),
        ],
    },
]);
Require(success.Committed && !success.RolledBack, "successful transaction must commit");
Require(success.Results.Count == 1 && Math.Abs(success.Results[0].After - 3000) < 0.001, "XP target must be reached exactly");
Require(Math.Abs((xpA + xpB) - 3000) < 0.001, "live XP slots must equal target after commit");
Require(!NumericRewardTransactionCore.NeedsMutation(xpA + xpB, 3000, false), "second automatic pass must be idempotent");

var standing = 0.20d;
var secondValue = 12000d;
var failOnce = true;
var rollback = NumericRewardTransactionCore.Execute([
    new NumericRewardTransactionRequest
    {
        QuestId = "first-change",
        Dimension = "TraderStanding",
        ExpectedBefore = 0.20,
        Target = 0.05,
        Slots = [new NumericRewardSlot(() => standing, value => standing = value)],
    },
    new NumericRewardTransactionRequest
    {
        QuestId = "forced-failure",
        Dimension = "Experience",
        ExpectedBefore = 12000,
        Target = 4000,
        Slots = [new NumericRewardSlot(
            () => secondValue,
            value =>
            {
                if (failOnce)
                {
                    failOnce = false;
                    throw new InvalidOperationException("synthetic setter failure");
                }
                secondValue = value;
            })],
    },
]);
Require(!rollback.Committed && rollback.RolledBack, "synthetic failure must roll back whole batch");
Require(rollback.Results.Count == 0, "rolled-back transaction must declare no committed results");
Require(Math.Abs(standing - 0.20) < 0.00001, "earlier standing mutation must be restored");
Require(Math.Abs(secondValue - 12000) < 0.001, "failing XP mutation must be restored");

Require(NumericRewardTransactionCore.NeedsMutation(9000, 3000, false), "automatic outlier above cap must mutate");
Require(!NumericRewardTransactionCore.NeedsMutation(2500, 3000, false), "automatic policy must never increase a normal value");
Require(NumericRewardTransactionCore.NeedsMutation(2500, 3000, true), "manual exact target may intentionally change either direction");

var itemPlan = ItemRewardStackPlanner.Plan(currentCount: 10, unitHandbookPrice: 25000, budgetCap: 100000);
Require(itemPlan.Eligible, "single known-price stack above budget must be eligible");
Require(itemPlan.TargetCount == 4, "item stack target must use floor(budget/unit price)");
Require(itemPlan.TargetHandbookValue == 100000, "item stack target value must stay within budget");

var alreadyNormalItem = ItemRewardStackPlanner.Plan(currentCount: 3, unitHandbookPrice: 25000, budgetCap: 100000);
Require(!alreadyNormalItem.Eligible && alreadyNormalItem.Reason == "AlreadyWithinBudget", "normal item stack must not be increased or changed");

var structuralRemovalRequired = ItemRewardStackPlanner.Plan(currentCount: 5, unitHandbookPrice: 25000, budgetCap: 10000);
Require(!structuralRemovalRequired.Eligible && structuralRemovalRequired.Reason == "BudgetBelowOneItemFloor", "planner must block cases requiring item removal/template replacement");

var singleItem = ItemRewardStackPlanner.Plan(currentCount: 1, unitHandbookPrice: 25000, budgetCap: 10000);
Require(!singleItem.Eligible && singleItem.Reason == "SingleItemCannotBeReducedWithoutStructuralRemoval", "single-item rewards must remain structural-protected");

var fractionalStack = ItemRewardStackPlanner.Plan(currentCount: 2.5, unitHandbookPrice: 25000, budgetCap: 10000);
Require(!fractionalStack.Eligible && fractionalStack.Reason == "NonIntegralStackCount", "non-integral stack counts must be blocked");

var itemStackCount = 10d;
var itemStackTx = NumericRewardTransactionCore.Execute([
    new NumericRewardTransactionRequest
    {
        QuestId = "single-stack-item-outlier",
        Dimension = "ItemRewardStackCount",
        ExpectedBefore = 10,
        Target = 4,
        Slots = [new NumericRewardSlot(() => itemStackCount, value => itemStackCount = value)],
    },
]);
Require(itemStackTx.Committed && !itemStackTx.RolledBack, "bounded single-stack item transaction must commit");
Require(itemStackCount == 4, "bounded item stack transaction must reach exact integer count");
Require(!NumericRewardTransactionCore.NeedsMutation(itemStackCount, 4, false), "bounded item stack second pass must be idempotent");

var mixedXp = 9000d;
var mixedStanding = 0.20d;
var mixedItemStack = 10d;
var itemFailOnce = true;
var mixedRollback = NumericRewardTransactionCore.Execute([
    new NumericRewardTransactionRequest
    {
        QuestId = "mixed-xp",
        Dimension = "Experience",
        ExpectedBefore = 9000,
        Target = 3000,
        Slots = [new NumericRewardSlot(() => mixedXp, value => mixedXp = value)],
    },
    new NumericRewardTransactionRequest
    {
        QuestId = "mixed-standing",
        Dimension = "TraderStanding",
        ExpectedBefore = 0.20,
        Target = 0.05,
        Slots = [new NumericRewardSlot(() => mixedStanding, value => mixedStanding = value)],
    },
    new NumericRewardTransactionRequest
    {
        QuestId = "mixed-item-failure",
        Dimension = "ItemRewardStackCount",
        ExpectedBefore = 10,
        Target = 4,
        Slots = [new NumericRewardSlot(
            () => mixedItemStack,
            value =>
            {
                if (itemFailOnce)
                {
                    itemFailOnce = false;
                    throw new InvalidOperationException("synthetic item-stack setter failure");
                }
                mixedItemStack = value;
            })],
    },
]);
Require(!mixedRollback.Committed && mixedRollback.RolledBack, "mixed numeric/item failure must roll back the whole batch");
Require(mixedRollback.Results.Count == 0, "mixed rolled-back batch must publish no committed mutations");
Require(Math.Abs(mixedXp - 9000) < 0.001, "mixed rollback must restore earlier XP mutation");
Require(Math.Abs(mixedStanding - 0.20) < 0.00001, "mixed rollback must restore earlier standing mutation");
Require(Math.Abs(mixedItemStack - 10) < 0.001, "mixed rollback must restore failing item stack mutation");

Console.WriteLine("Economy Admiral Enforce transaction + bounded item stack planner smoke PASS");
