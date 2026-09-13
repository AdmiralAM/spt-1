namespace Admiral.SecondLife;

public interface IReversibleInventoryMove
{
    string ItemId { get; }
    bool CanExecute();
    bool Execute();
    void RollBack();
}

public sealed class ArmamentTransferTransaction
{
    private readonly IReversibleInventoryMove[] moves;

    public ArmamentTransferTransaction(
        EmergencyArmamentPlan plan,
        IReversibleInventoryMove pistolMove,
        IReversibleInventoryMove installedMagazineMove,
        IReversibleInventoryMove spareMagazineMove)
    {
        moves = new[]
        {
            pistolMove ?? throw new ArgumentNullException(nameof(pistolMove)),
            installedMagazineMove ?? throw new ArgumentNullException(nameof(installedMagazineMove)),
            spareMagazineMove ?? throw new ArgumentNullException(nameof(spareMagazineMove))
        };

        string[] expected =
        {
            plan.PistolItemId,
            plan.InstalledMagazineItemId,
            plan.SpareMagazineItemId
        };
        string[] actual = moves.Select(move => move.ItemId).ToArray();
        if (expected.Any(string.IsNullOrWhiteSpace) ||
            expected.Distinct(StringComparer.Ordinal).Count() != expected.Length ||
            !expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new ArgumentException("Transfer moves must match the three distinct stash item IDs in the armament plan.");
        }
    }

    public bool TryExecute()
    {
        if (moves.Any(move => !move.CanExecute())) return false;

        int completed = 0;
        try
        {
            for (; completed < moves.Length; completed++)
            {
                if (!moves[completed].Execute())
                {
                    moves[completed].RollBack();
                    RollBackCompleted(completed);
                    return false;
                }
            }

            return true;
        }
        catch
        {
            if (completed < moves.Length)
            {
                TryRollBack(moves[completed]);
            }
            RollBackCompleted(completed);
            return false;
        }
    }

    private void RollBackCompleted(int completed)
    {
        for (int index = completed - 1; index >= 0; index--)
        {
            TryRollBack(moves[index]);
        }
    }

    private static void TryRollBack(IReversibleInventoryMove move)
    {
        try { move.RollBack(); }
        catch { }
    }
}
