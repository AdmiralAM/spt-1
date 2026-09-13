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
Expect(!lifecycle.TryApply(RecoveryTransition.EnableRaid), "raid identity is required");
Expect(lifecycle.TryApply(RecoveryTransition.EnableRaid, raidId: "raid-1"), "raid enables");
Expect(lifecycle.Snapshot.RaidId == "raid-1", "raid identity is pinned");
Expect(!lifecycle.TryApply(RecoveryTransition.EnableRaid, raidId: "raid-2"), "active raid cannot be replaced");
Expect(!lifecycle.TryApply(RecoveryTransition.ConfirmRecoverySpawn, emergencyLoadoutId: "kit-1"), "spawn cannot bypass death");
Expect(lifecycle.TryApply(RecoveryTransition.CaptureFirstDeath, originalCorpseId: "corpse-1"), "first death captured");
Expect(!lifecycle.TryApply(RecoveryTransition.CaptureFirstDeath, originalCorpseId: "corpse-1"), "duplicate death callback is inert");
Expect(lifecycle.Snapshot.OriginalCorpseId == "corpse-1", "corpse identity preserved");
Expect(lifecycle.TryApply(RecoveryTransition.ArmRecovery), "recovery armed");
Expect(!lifecycle.TryApply(RecoveryTransition.ArmRecovery), "duplicate arm callback is inert");
Expect(!lifecycle.TryApply(RecoveryTransition.ConfirmRecoverySpawn, emergencyLoadoutId: ""), "loadout identity required");
Expect(lifecycle.TryApply(RecoveryTransition.ConfirmRecoverySpawn, emergencyLoadoutId: "kit-1"), "recovery spawned");
Expect(!lifecycle.TryApply(RecoveryTransition.ConfirmRecoverySpawn, emergencyLoadoutId: "kit-2"), "duplicate spawn callback is inert");
Expect(lifecycle.Snapshot.RecoveryConsumed, "recovery consumed exactly once");
Expect(!lifecycle.TryApply(RecoveryTransition.CaptureFirstDeath, originalCorpseId: "corpse-2"), "second recovery rejected");
Expect(lifecycle.TryApply(RecoveryTransition.Disable), "disable is accepted");
Expect(!lifecycle.TryApply(RecoveryTransition.Disable), "duplicate disable callback is inert");
Expect(!lifecycle.TryApply(RecoveryTransition.EnableRaid, raidId: "raid-1"), "disable-enable cannot regrant the same raid");
Expect(lifecycle.Snapshot.RecoveryConsumed, "disable preserves consumed accounting");
Expect(lifecycle.TryApply(RecoveryTransition.EnableRaid, raidId: "raid-after-disable"), "a distinct later raid can start after disable");
Expect(!lifecycle.Snapshot.RecoveryConsumed, "distinct later raid gets fresh accounting");

var finalDeath = new RecoveryStateMachine();
Expect(finalDeath.TryApply(RecoveryTransition.EnableRaid, raidId: "raid-final"), "final-death raid enables");
Expect(finalDeath.TryApply(RecoveryTransition.CaptureFirstDeath, originalCorpseId: "corpse-final"), "final-death first death captured");
Expect(finalDeath.TryApply(RecoveryTransition.ArmRecovery), "final-death recovery armed");
Expect(finalDeath.TryApply(RecoveryTransition.ConfirmRecoverySpawn, emergencyLoadoutId: "kit-final"), "final-death recovery spawned");
Expect(finalDeath.TryApply(RecoveryTransition.CaptureFinalDeath), "final death accepted");
Expect(!finalDeath.TryApply(RecoveryTransition.EnableRaid, raidId: "raid-final"), "terminal state cannot restart the same raid");
Expect(finalDeath.TryApply(RecoveryTransition.EnableRaid, raidId: "raid-next"), "new raid identity starts fresh after terminal state");
Expect(!finalDeath.Snapshot.RecoveryConsumed, "new raid receives its own one-use accounting");

var extraction = new RecoveryStateMachine();
Expect(extraction.TryApply(RecoveryTransition.EnableRaid, raidId: "raid-extract"), "second raid enables");
Expect(extraction.TryApply(RecoveryTransition.Extract), "normal extraction accepted");
Expect(extraction.Snapshot.State == RecoveryState.Extracted, "normal extraction terminal");
Expect(!extraction.TryApply(RecoveryTransition.Extract), "duplicate extraction callback is inert");

var failedPreflight = new RecoveryStateMachine();
Expect(failedPreflight.TryApply(RecoveryTransition.EnableRaid, raidId: "raid-fail"), "failed-preflight raid enables");
Expect(failedPreflight.TryApply(RecoveryTransition.CaptureFirstDeath, originalCorpseId: "corpse-fail"), "failed-preflight death captured");
Expect(failedPreflight.TryApply(RecoveryTransition.AbortToNativeDeath), "failed preflight returns to native death");
Expect(failedPreflight.Snapshot.State == RecoveryState.FinalDeath, "failed preflight is terminal");
Expect(!failedPreflight.TryApply(RecoveryTransition.ArmRecovery), "failed preflight cannot arm later");

var spawnCandidates = new[]
{
    new SpawnCandidate("original", new WorldPoint(0, 0, 0), true, false),
    new SpawnCandidate("corpse-near", new WorldPoint(10, 0, 0), true, false),
    new SpawnCandidate("killer-near", new WorldPoint(100, 0, 0), true, false),
    new SpawnCandidate("combat-near", new WorldPoint(200, 0, 0), true, false),
    new SpawnCandidate("invalid-extract", new WorldPoint(300, 0, 0), true, true),
    new SpawnCandidate("native-invalid", new WorldPoint(400, 0, 0), false, false),
    new SpawnCandidate("safe-a", new WorldPoint(500, 0, 0), true, false),
    new SpawnCandidate("safe-b", new WorldPoint(600, 0, 0), true, false)
};
var spawnPolicy = new SafeSpawnPolicy(50, 50, 50);
Expect(SafeSpawnSelector.TrySelect(
    spawnCandidates,
    "original",
    new WorldPoint(0, 0, 0),
    new WorldPoint(100, 0, 0),
    new[] { new WorldPoint(200, 0, 0) },
    spawnPolicy,
    42,
    out SpawnCandidate firstSpawn), "safe alternate spawn exists");
Expect(firstSpawn.Id is "safe-a" or "safe-b", "unsafe spawn points are excluded");
Expect(SafeSpawnSelector.TrySelect(
    spawnCandidates.Reverse(),
    "original",
    new WorldPoint(0, 0, 0),
    new WorldPoint(100, 0, 0),
    new[] { new WorldPoint(200, 0, 0) },
    spawnPolicy,
    42,
    out SpawnCandidate repeatedSpawn), "reordered input still selects");
Expect(repeatedSpawn.Id == firstSpawn.Id, "seeded spawn selection is deterministic and order independent");
Expect(!SafeSpawnSelector.TrySelect(
    spawnCandidates.Take(6),
    "original",
    new WorldPoint(0, 0, 0),
    new WorldPoint(100, 0, 0),
    new[] { new WorldPoint(200, 0, 0) },
    spawnPolicy,
    42,
    out _), "no safe point fails closed");

var stashPistols = new[]
{
    new StashPistol("pistol-item-b", "pistol-template-b", new StashMagazine("installed-b", "mag-template-b"), new[]
    {
        new StashMagazine("spare-b2", "mag-template-b"),
        new StashMagazine("spare-b1", "mag-template-b"),
        new StashMagazine("spare-b1", "mag-template-b")
    }),
    new StashPistol("pistol-item-invalid", "pistol-template-invalid", null, Array.Empty<StashMagazine>()),
    new StashPistol("pistol-item-a", "pistol-template-a", new StashMagazine("installed-a", "mag-template-a"), new[]
    {
        new StashMagazine("spare-a", "mag-template-a")
    })
};
Expect(EmergencyArmamentSelector.TrySelectFromStash(stashPistols, 73, out EmergencyArmamentPlan armament), "valid stash pistol plan exists");
Expect(armament.PistolItemId is "pistol-item-a" or "pistol-item-b", "invalid stash pistol is excluded");
Expect(armament.InstalledMagazineTemplateId == armament.SpareMagazineTemplateId, "installed and spare stash magazines are compatible");
Expect(armament.PistolItemId != armament.InstalledMagazineItemId && armament.InstalledMagazineItemId != armament.SpareMagazineItemId, "existing stash item identities remain distinct");
Expect(EmergencyArmamentSelector.TrySelectFromStash(stashPistols.Reverse(), 73, out EmergencyArmamentPlan repeatedArmament), "reordered stash input still selects");
Expect(repeatedArmament == armament, "seeded pistol selection is deterministic and order independent");
Expect(!EmergencyArmamentSelector.TrySelectFromStash(
    new[] { new StashPistol("pistol-item-invalid", "pistol-template-invalid", null, Array.Empty<StashMagazine>()) },
    73,
    out _), "no complete stash set means unarmed recovery");

var moved = new List<string>();
var rolledBack = new List<string>();
var successfulTransfer = new ArmamentTransferTransaction(
    armament,
    new FakeMove(armament.PistolItemId, true, true, moved, rolledBack),
    new FakeMove(armament.InstalledMagazineItemId, true, true, moved, rolledBack),
    new FakeMove(armament.SpareMagazineItemId, true, true, moved, rolledBack));
Expect(successfulTransfer.TryExecute(), "complete stash transfer commits");
Expect(moved.SequenceEqual(new[] { armament.PistolItemId, armament.InstalledMagazineItemId, armament.SpareMagazineItemId }), "stash items move exactly once in plan order");
Expect(rolledBack.Count == 0, "successful stash transfer does not roll back");

moved.Clear();
rolledBack.Clear();
var failedTransfer = new ArmamentTransferTransaction(
    armament,
    new FakeMove(armament.PistolItemId, true, true, moved, rolledBack),
    new FakeMove(armament.InstalledMagazineItemId, true, false, moved, rolledBack),
    new FakeMove(armament.SpareMagazineItemId, true, true, moved, rolledBack));
Expect(!failedTransfer.TryExecute(), "partial stash transfer fails closed");
Expect(moved.SequenceEqual(new[] { armament.PistolItemId }), "later stash move is not attempted after failure");
Expect(rolledBack.SequenceEqual(new[] { armament.InstalledMagazineItemId, armament.PistolItemId }), "failed and completed moves roll back in reverse order");

moved.Clear();
rolledBack.Clear();
var failedPreflightTransfer = new ArmamentTransferTransaction(
    armament,
    new FakeMove(armament.PistolItemId, true, true, moved, rolledBack),
    new FakeMove(armament.InstalledMagazineItemId, false, true, moved, rolledBack),
    new FakeMove(armament.SpareMagazineItemId, true, true, moved, rolledBack));
Expect(!failedPreflightTransfer.TryExecute(), "all stash moves preflight before mutation");
Expect(moved.Count == 0 && rolledBack.Count == 0, "failed transfer preflight is mutation-free");

Console.WriteLine($"Second Life foundation PASS: {assertions} assertions.");

sealed class FakeMove : IReversibleInventoryMove
{
    private readonly bool canExecute;
    private readonly bool executeResult;
    private readonly List<string> moved;
    private readonly List<string> rolledBack;

    public FakeMove(string itemId, bool canExecute, bool executeResult, List<string> moved, List<string> rolledBack)
    {
        ItemId = itemId;
        this.canExecute = canExecute;
        this.executeResult = executeResult;
        this.moved = moved;
        this.rolledBack = rolledBack;
    }

    public string ItemId { get; }
    public bool CanExecute() => canExecute;

    public bool Execute()
    {
        if (executeResult) moved.Add(ItemId);
        return executeResult;
    }

    public void RollBack() => rolledBack.Add(ItemId);
}
