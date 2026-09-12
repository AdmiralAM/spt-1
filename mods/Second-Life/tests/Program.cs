using Admiral.SecondLife;

var assertions = 0;
void Expect(bool condition, string message)
{
    assertions++;
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

var lifecycle = new RecoveryStateMachine();
Expect(lifecycle.Snapshot.State == RecoveryState.Disabled, "starts disabled");
Expect(!lifecycle.TryApply(RecoveryTransition.CaptureFirstDeath, "corpse-1"), "disabled is inert");
Expect(lifecycle.TryApply(RecoveryTransition.EnableRaid), "raid enables");
Expect(!lifecycle.TryApply(RecoveryTransition.ConfirmRecoverySpawn, emergencyLoadoutId: "kit-1"), "spawn cannot bypass death");
Expect(lifecycle.TryApply(RecoveryTransition.CaptureFirstDeath, "corpse-1"), "first death captured");
Expect(lifecycle.Snapshot.OriginalCorpseId == "corpse-1", "corpse identity preserved");
Expect(lifecycle.TryApply(RecoveryTransition.ArmRecovery), "recovery armed");
Expect(!lifecycle.TryApply(RecoveryTransition.ConfirmRecoverySpawn, emergencyLoadoutId: ""), "loadout identity required");
Expect(lifecycle.TryApply(RecoveryTransition.ConfirmRecoverySpawn, emergencyLoadoutId: "kit-1"), "recovery spawned");
Expect(lifecycle.Snapshot.RecoveryConsumed, "recovery consumed exactly once");
Expect(!lifecycle.TryApply(RecoveryTransition.CaptureFirstDeath, "corpse-2"), "second recovery rejected");
Expect(lifecycle.TryApply(RecoveryTransition.CaptureFinalDeath), "final death accepted");
Expect(!lifecycle.TryApply(RecoveryTransition.EnableRaid), "terminal state cannot restart silently");

var extraction = new RecoveryStateMachine();
Expect(extraction.TryApply(RecoveryTransition.EnableRaid), "second raid enables");
Expect(extraction.TryApply(RecoveryTransition.Extract), "normal extraction accepted");
Expect(extraction.Snapshot.State == RecoveryState.Extracted, "normal extraction terminal");

Console.WriteLine($"Second Life foundation PASS: {assertions} assertions.");
