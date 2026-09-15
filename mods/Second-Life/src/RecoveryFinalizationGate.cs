namespace Admiral.SecondLife;

public enum NativeFinalizationDecision
{
    ContinueNative,
    SuppressForRecovery,
    SuppressDuplicate
}

public sealed class RecoveryFinalizationGate
{
    private readonly RecoveryStateMachine lifecycle = new();

    public RecoverySnapshot Snapshot => lifecycle.Snapshot;

    public NativeFinalizationDecision HandleDeathBoundary(
        string raidId,
        string corpseId,
        bool executorReady)
    {
        RecoverySnapshot current = lifecycle.Snapshot;
        if (current.State == RecoveryState.RecoveryPending &&
            string.Equals(current.RaidId, raidId, StringComparison.Ordinal) &&
            string.Equals(current.OriginalCorpseId, corpseId, StringComparison.Ordinal))
        {
            return NativeFinalizationDecision.SuppressDuplicate;
        }

        if (current.State == RecoveryState.RecoverySpawned &&
            string.Equals(current.RaidId, raidId, StringComparison.Ordinal))
        {
            lifecycle.TryApply(RecoveryTransition.CaptureFinalDeath);
            return NativeFinalizationDecision.ContinueNative;
        }

        if (!EnsureRaid(raidId)) return NativeFinalizationDecision.ContinueNative;
        if (!lifecycle.TryApply(RecoveryTransition.CaptureFirstDeath, originalCorpseId: corpseId))
            return NativeFinalizationDecision.ContinueNative;

        if (!executorReady || !lifecycle.TryApply(RecoveryTransition.ArmRecovery))
        {
            lifecycle.TryApply(RecoveryTransition.AbortToNativeDeath);
            return NativeFinalizationDecision.ContinueNative;
        }

        return NativeFinalizationDecision.SuppressForRecovery;
    }

    public bool ConfirmRecovery(string emergencyLoadoutId) =>
        lifecycle.TryApply(
            RecoveryTransition.ConfirmRecoverySpawn,
            emergencyLoadoutId: emergencyLoadoutId);

    public bool AbortPendingRecovery() =>
        lifecycle.TryApply(RecoveryTransition.AbortToNativeDeath);

    public bool Disable() => lifecycle.TryApply(RecoveryTransition.Disable);

    private bool EnsureRaid(string raidId)
    {
        RecoverySnapshot current = lifecycle.Snapshot;
        if (string.Equals(current.RaidId, raidId, StringComparison.Ordinal) &&
            current.State != RecoveryState.Disabled)
        {
            return true;
        }

        return lifecycle.TryApply(RecoveryTransition.EnableRaid, raidId: raidId);
    }
}
