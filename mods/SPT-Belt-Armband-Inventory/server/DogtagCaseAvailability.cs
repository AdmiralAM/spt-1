namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Process-local publication gate for the optional Dogtag Case component.
/// A foreign mod may legitimately extend EFT's canonical dogtag taxonomy before
/// our preload boundary. Until that combined contract is validated, B&amp;A&amp;HB
/// leaves the component unpublished instead of preventing the rest of the mod
/// stack from starting.
/// </summary>
internal static class DogtagCaseAvailability
{
    private static string? unavailableReason;

    internal static bool IsAvailable => unavailableReason == null;

    internal static string UnavailableReason => unavailableReason ?? string.Empty;

    internal static void MarkUnavailable(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An unavailable Dogtag Case component requires a diagnostic reason.", nameof(reason));

        Interlocked.CompareExchange(ref unavailableReason, reason, null);
    }
}
