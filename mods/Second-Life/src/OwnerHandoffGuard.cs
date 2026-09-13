namespace Admiral.SecondLife;

public static class OwnerHandoffGuard
{
    public static bool MustPreserveReplacement(object currentPlayer, object cleanupOwnerPlayer) =>
        currentPlayer != null && !ReferenceEquals(currentPlayer, cleanupOwnerPlayer);
}
