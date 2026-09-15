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
    AbortToNativeDeath,
    CaptureFinalDeath,
    Extract
}

public readonly record struct RecoverySnapshot(
    RecoveryState State,
    string? RaidId,
    bool RecoveryConsumed,
    string? OriginalCorpseId,
    string? EmergencyLoadoutId);

public sealed class RecoveryStateMachine
{
    public RecoverySnapshot Snapshot { get; private set; } =
        new(RecoveryState.Disabled, null, false, null, null);

    public bool TryApply(
        RecoveryTransition transition,
        string? raidId = null,
        string? originalCorpseId = null,
        string? emergencyLoadoutId = null)
    {
        RecoverySnapshot current = Snapshot;
        RecoverySnapshot? next = (current.State, transition) switch
        {
            (RecoveryState.Disabled, RecoveryTransition.EnableRaid)
                when IsIdentity(raidId) && !string.Equals(current.RaidId, raidId, StringComparison.Ordinal) =>
                new(RecoveryState.Alive, raidId, false, null, null),

            (RecoveryState.FinalDeath or RecoveryState.Extracted, RecoveryTransition.EnableRaid)
                when IsIdentity(raidId) && !string.Equals(current.RaidId, raidId, StringComparison.Ordinal) =>
                new(RecoveryState.Alive, raidId, false, null, null),

            (_, RecoveryTransition.Disable) when current.State != RecoveryState.Disabled =>
                new(RecoveryState.Disabled, current.RaidId, current.RecoveryConsumed, current.OriginalCorpseId, current.EmergencyLoadoutId),

            (RecoveryState.Alive, RecoveryTransition.CaptureFirstDeath)
                when IsIdentity(originalCorpseId) && !current.RecoveryConsumed =>
                new(RecoveryState.FirstDeathCaptured, current.RaidId, false, originalCorpseId, null),

            (RecoveryState.FirstDeathCaptured, RecoveryTransition.ArmRecovery)
                when IsIdentity(current.OriginalCorpseId) =>
                new(RecoveryState.RecoveryPending, current.RaidId, false, current.OriginalCorpseId, null),

            (RecoveryState.RecoveryPending, RecoveryTransition.ConfirmRecoverySpawn)
                when IsIdentity(current.OriginalCorpseId) && IsIdentity(emergencyLoadoutId) =>
                new(RecoveryState.RecoverySpawned, current.RaidId, true, current.OriginalCorpseId, emergencyLoadoutId),

            (RecoveryState.RecoverySpawned, RecoveryTransition.CaptureFinalDeath) =>
                new(RecoveryState.FinalDeath, current.RaidId, true, current.OriginalCorpseId, current.EmergencyLoadoutId),

            (RecoveryState.Alive or RecoveryState.FirstDeathCaptured or RecoveryState.RecoveryPending, RecoveryTransition.AbortToNativeDeath) =>
                new(RecoveryState.FinalDeath, current.RaidId, current.RecoveryConsumed, current.OriginalCorpseId, current.EmergencyLoadoutId),

            (RecoveryState.Alive or RecoveryState.RecoverySpawned, RecoveryTransition.Extract) =>
                new(RecoveryState.Extracted, current.RaidId, current.RecoveryConsumed, current.OriginalCorpseId, current.EmergencyLoadoutId),

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
