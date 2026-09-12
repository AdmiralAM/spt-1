namespace Admiral.SecondLife;

public enum RecoveryState
{
    Disabled,
    Alive,
    FirstDeathCaptured,
    RecoveryPending,
    RecoverySpawned,
    FinalDeath,
    Extracted
}

public enum RecoveryTransition
{
    EnableRaid,
    Disable,
    CaptureFirstDeath,
    ArmRecovery,
    ConfirmRecoverySpawn,
    CaptureFinalDeath,
    Extract
}

public readonly record struct RecoverySnapshot(
    RecoveryState State,
    bool RecoveryConsumed,
    string? OriginalCorpseId,
    string? EmergencyLoadoutId);

public sealed class RecoveryStateMachine
{
    public RecoverySnapshot Snapshot { get; private set; } =
        new(RecoveryState.Disabled, false, null, null);

    public bool TryApply(
        RecoveryTransition transition,
        string? originalCorpseId = null,
        string? emergencyLoadoutId = null)
    {
        RecoverySnapshot current = Snapshot;
        RecoverySnapshot? next = (current.State, transition) switch
        {
            (RecoveryState.Disabled, RecoveryTransition.EnableRaid) =>
                new(RecoveryState.Alive, false, null, null),

            (_, RecoveryTransition.Disable) =>
                new(RecoveryState.Disabled, current.RecoveryConsumed, current.OriginalCorpseId, current.EmergencyLoadoutId),

            (RecoveryState.Alive, RecoveryTransition.CaptureFirstDeath)
                when IsIdentity(originalCorpseId) && !current.RecoveryConsumed =>
                new(RecoveryState.FirstDeathCaptured, false, originalCorpseId, null),

            (RecoveryState.FirstDeathCaptured, RecoveryTransition.ArmRecovery)
                when IsIdentity(current.OriginalCorpseId) =>
                new(RecoveryState.RecoveryPending, false, current.OriginalCorpseId, null),

            (RecoveryState.RecoveryPending, RecoveryTransition.ConfirmRecoverySpawn)
                when IsIdentity(current.OriginalCorpseId) && IsIdentity(emergencyLoadoutId) =>
                new(RecoveryState.RecoverySpawned, true, current.OriginalCorpseId, emergencyLoadoutId),

            (RecoveryState.RecoverySpawned, RecoveryTransition.CaptureFinalDeath) =>
                new(RecoveryState.FinalDeath, true, current.OriginalCorpseId, current.EmergencyLoadoutId),

            (RecoveryState.Alive or RecoveryState.RecoverySpawned, RecoveryTransition.Extract) =>
                new(RecoveryState.Extracted, current.RecoveryConsumed, current.OriginalCorpseId, current.EmergencyLoadoutId),

            _ => null
        };

        if (next is null)
        {
            return false;
        }

        Snapshot = next.Value;
        return true;
    }

    private static bool IsIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value);
}
