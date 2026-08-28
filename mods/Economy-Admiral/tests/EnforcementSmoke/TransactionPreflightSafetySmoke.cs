using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class TransactionPreflightSafetySmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        FullBatchPreflightMustFinishBeforeFirstWrite();
        NonFiniteSourceMustFailClosedBeforeWrites();
        NonFinitePostWriteStateMustRollback();
    }

    private static void FullBatchPreflightMustFinishBeforeFirstWrite()
    {
        var first = 100d;
        var second = 50d;
        var writes = 0;
        var outcome = NumericRewardTransactionCore.Execute([
            new NumericRewardTransactionRequest
            {
                QuestId = "preflight-first-valid",
                Dimension = "Experience",
                ExpectedBefore = 100,
                Target = 25,
                Slots = [new NumericRewardSlot(() => first, value => { writes++; first = value; })],
            },
            new NumericRewardTransactionRequest
            {
                QuestId = "preflight-second-drifted",
                Dimension = "Experience",
                ExpectedBefore = 60,
                Target = 20,
                Slots = [new NumericRewardSlot(() => second, value => { writes++; second = value; })],
            },
        ]);

        Require(!outcome.Committed, "batch with later preflight drift must not commit");
        Require(!outcome.RolledBack, "preflight failure before writes must not claim rollback");
        Require(outcome.Error?.Contains("Preflight failed before writes", StringComparison.Ordinal) == true, "preflight failure must be classified explicitly");
        Require(writes == 0, "later request preflight failure must occur before any earlier request write");
        Require(first == 100 && second == 50, "full-batch preflight failure must preserve every source value");
    }

    private static void NonFiniteSourceMustFailClosedBeforeWrites()
    {
        var first = 100d;
        var writes = 0;
        var outcome = NumericRewardTransactionCore.Execute([
            new NumericRewardTransactionRequest
            {
                QuestId = "finite-before-nan",
                Dimension = "TraderStanding",
                ExpectedBefore = 100,
                Target = 10,
                Slots = [new NumericRewardSlot(() => first, value => { writes++; first = value; })],
            },
            new NumericRewardTransactionRequest
            {
                QuestId = "nan-source",
                Dimension = "Experience",
                ExpectedBefore = 1,
                Target = 1,
                Slots = [new NumericRewardSlot(() => double.NaN, _ => writes++)],
            },
        ]);

        Require(!outcome.Committed && !outcome.RolledBack, "non-finite source must abort in preflight without rollback");
        Require(outcome.Error?.Contains("non-finite", StringComparison.OrdinalIgnoreCase) == true, "non-finite preflight source must be reported");
        Require(writes == 0 && first == 100, "NaN source must never allow an earlier request to write");
    }

    private static void NonFinitePostWriteStateMustRollback()
    {
        var value = 100d;
        var corruptOnTargetWrite = true;
        var outcome = NumericRewardTransactionCore.Execute([
            new NumericRewardTransactionRequest
            {
                QuestId = "nan-after-write",
                Dimension = "Experience",
                ExpectedBefore = 100,
                Target = 50,
                Slots = [new NumericRewardSlot(
                    () => value,
                    target =>
                    {
                        if (corruptOnTargetWrite && Math.Abs(target - 50) < 0.001)
                        {
                            corruptOnTargetWrite = false;
                            value = double.NaN;
                            return;
                        }
                        value = target;
                    })],
            },
        ]);

        Require(!outcome.Committed && outcome.RolledBack, "non-finite state produced during apply must trigger proven rollback");
        Require(value == 100, "rollback must restore source after a non-finite post-write state");
        Require(outcome.Results.Count == 0, "rolled-back non-finite apply must publish no committed results");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Economy Admiral transaction preflight safety smoke: {message}");
    }
}
