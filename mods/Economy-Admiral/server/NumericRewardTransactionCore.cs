namespace SPTEconomy;

public sealed class NumericRewardSlot(Func<double> read, Action<double> write)
{
    public double Read() => read();
    public void Write(double value) => write(value);
}

public sealed record NumericRewardTransactionRequest
{
    public required string QuestId { get; init; }
    public required string Dimension { get; init; }
    public required double ExpectedBefore { get; init; }
    public required double Target { get; init; }
    public required IReadOnlyList<NumericRewardSlot> Slots { get; init; }
}

public sealed record NumericRewardTransactionResult
{
    public required string QuestId { get; init; }
    public required string Dimension { get; init; }
    public required double Before { get; init; }
    public required double Target { get; init; }
    public required double After { get; init; }
}

public sealed record NumericRewardTransactionOutcome
{
    public required bool Committed { get; init; }
    public required bool RolledBack { get; init; }
    public string? Error { get; init; }
    public required IReadOnlyList<NumericRewardTransactionResult> Results { get; init; }
}

public static class NumericRewardTransactionCore
{
    public static NumericRewardTransactionOutcome Execute(IReadOnlyList<NumericRewardTransactionRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var journal = new List<JournalEntry>();
        var results = new List<NumericRewardTransactionResult>();

        try
        {
            foreach (var request in requests)
            {
                ValidateRequest(request);
                var beforeSlots = request.Slots.Select(slot => slot.Read()).ToArray();
                var beforeTotal = beforeSlots.Sum();
                if (Math.Abs(beforeTotal - request.ExpectedBefore) > Tolerance(request.Dimension))
                    throw new InvalidOperationException($"'{request.QuestId}' {request.Dimension} drifted before apply: expected={request.ExpectedBefore}, actual={beforeTotal}.");

                var afterSlots = ScaleSlots(beforeSlots, request.Target, request.Dimension);
                var entry = new JournalEntry(request, beforeSlots);
                journal.Add(entry); // Journal before first write so a mid-request failure is rollback-safe.

                for (var index = 0; index < request.Slots.Count; index++)
                    request.Slots[index].Write(afterSlots[index]);

                var after = request.Slots.Sum(slot => slot.Read());
                if (Math.Abs(after - request.Target) > Tolerance(request.Dimension))
                    throw new InvalidOperationException($"'{request.QuestId}' {request.Dimension} target verification failed: target={request.Target}, actual={after}.");

                results.Add(new NumericRewardTransactionResult
                {
                    QuestId = request.QuestId,
                    Dimension = request.Dimension,
                    Before = beforeTotal,
                    Target = request.Target,
                    After = after,
                });
            }

            return new NumericRewardTransactionOutcome
            {
                Committed = true,
                RolledBack = false,
                Results = results,
            };
        }
        catch (Exception exception)
        {
            Rollback(journal);
            VerifyRollback(journal);
            return new NumericRewardTransactionOutcome
            {
                Committed = false,
                RolledBack = true,
                Error = exception.Message,
                Results = Array.Empty<NumericRewardTransactionResult>(),
            };
        }
    }

    public static double[] ScaleSlots(IReadOnlyList<double> before, double target, string dimension)
    {
        if (before.Count == 0) throw new InvalidOperationException("Cannot scale an empty reward set.");
        if (!double.IsFinite(target)) throw new InvalidOperationException("Numeric reward target must be finite.");

        if (dimension == "ItemRewardStackCount")
        {
            if (before.Count != 1) throw new InvalidOperationException("Item stack mutation supports exactly one writable reward-item stack.");
            if (target < 1 || Math.Abs(target - Math.Round(target, 0)) > 0.000001)
                throw new InvalidOperationException("Item reward stack target must be an integer >= 1.");
            return [Math.Round(target, 0)];
        }

        var total = before.Sum();
        if (Math.Abs(total) < 0.0000001)
        {
            if (before.Count != 1) throw new InvalidOperationException("Cannot deterministically distribute a non-zero target across multiple zero-valued reward records.");
            return [Round(target, dimension)];
        }

        var result = new double[before.Count];
        var assigned = 0d;
        for (var index = 0; index < before.Count - 1; index++)
        {
            result[index] = Round(before[index] / total * target, dimension);
            assigned += result[index];
        }
        result[^1] = Round(target - assigned, dimension);
        return result;
    }

    public static bool NeedsMutation(double current, double target, bool manualExact)
    {
        if (!double.IsFinite(current) || !double.IsFinite(target)) return false;
        if (manualExact) return Math.Abs(current - target) > 0.0000001;
        return Math.Abs(current) > Math.Abs(target) + 0.0000001;
    }

    private static void ValidateRequest(NumericRewardTransactionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QuestId)) throw new InvalidOperationException("Transaction quest id must not be empty.");
        if (request.Dimension is not ("Experience" or "TraderStanding" or "ItemRewardStackCount"))
            throw new InvalidOperationException($"Unsupported numeric reward dimension '{request.Dimension}'.");
        if (request.Slots.Count == 0) throw new InvalidOperationException($"'{request.QuestId}' {request.Dimension} has no writable slots.");
        if (!double.IsFinite(request.ExpectedBefore) || !double.IsFinite(request.Target)) throw new InvalidOperationException("Transaction values must be finite.");
    }

    private static void Rollback(IEnumerable<JournalEntry> journal)
    {
        foreach (var entry in journal.Reverse())
            for (var index = 0; index < entry.Request.Slots.Count; index++)
                entry.Request.Slots[index].Write(entry.BeforeSlots[index]);
    }

    private static void VerifyRollback(IEnumerable<JournalEntry> journal)
    {
        foreach (var entry in journal)
            for (var index = 0; index < entry.Request.Slots.Count; index++)
            {
                var actual = entry.Request.Slots[index].Read();
                if (Math.Abs(actual - entry.BeforeSlots[index]) > Tolerance(entry.Request.Dimension))
                    throw new InvalidOperationException($"Rollback verification failed for '{entry.Request.QuestId}' {entry.Request.Dimension} slot {index}.");
            }
    }

    private static double Round(double value, string dimension) => Math.Round(value, dimension is "Experience" or "ItemRewardStackCount" ? 0 : 4);
    private static double Tolerance(string dimension) => dimension is "Experience" or "ItemRewardStackCount" ? 0.001 : 0.00001;
    private sealed record JournalEntry(NumericRewardTransactionRequest Request, double[] BeforeSlots);
}
