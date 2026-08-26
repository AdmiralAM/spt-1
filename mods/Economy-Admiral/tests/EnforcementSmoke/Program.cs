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

Console.WriteLine("Economy Admiral Enforce transaction smoke PASS");
