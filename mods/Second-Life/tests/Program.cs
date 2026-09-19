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

var consecutiveRaids = new RecoveryFinalizationGate();
Expect(consecutiveRaids.StartRaid("consecutive-a"), "native first raid start enables recovery");
Expect(consecutiveRaids.HandleDeathBoundary("consecutive-a", "corpse-a", executorReady: true) == NativeFinalizationDecision.SuppressForRecovery, "first consecutive raid captures death");
Expect(consecutiveRaids.ConfirmRecovery("loadout-a"), "first consecutive raid consumes its recovery");
Expect(consecutiveRaids.StartRaid("consecutive-b"), "native next raid start resets a spawned prior recovery");
Expect(consecutiveRaids.Snapshot.State == RecoveryState.Alive && !consecutiveRaids.Snapshot.RecoveryConsumed, "next raid receives a fresh recovery before any death");
Expect(consecutiveRaids.HandleDeathBoundary("consecutive-b", "corpse-b", executorReady: true) == NativeFinalizationDecision.SuppressForRecovery, "next consecutive raid captures its own first death");

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
var compactMapPolicy = SafeSpawnSelector.AdaptPolicyToMap(
    new[]
    {
        new SpawnCandidate("compact-a", new WorldPoint(0, 0, 0), true, false),
        new SpawnCandidate("compact-b", new WorldPoint(100, 0, 100), true, false)
    },
    new SafeSpawnPolicy(75, 100, 75));
Expect(compactMapPolicy.MinimumCorpseDistance < 100, "compact map scales corpse clearance below the large-map setting");
Expect(compactMapPolicy.MinimumKillerDistance < 75, "compact map scales killer clearance below the large-map setting");
Expect(compactMapPolicy.MinimumCombatDistance < compactMapPolicy.MinimumKillerDistance, "compact map keeps ordinary combatants less restrictive than the killer");
var largeMapPolicy = SafeSpawnSelector.AdaptPolicyToMap(
    new[]
    {
        new SpawnCandidate("large-a", new WorldPoint(0, 0, 0), true, false),
        new SpawnCandidate("large-b", new WorldPoint(1000, 0, 1000), true, false)
    },
    new SafeSpawnPolicy(75, 100, 75));
Expect(largeMapPolicy == new SafeSpawnPolicy(75, 100, 75), "large map retains configured safety distances");
var nestedInventory = new Dictionary<string, string?>(StringComparer.Ordinal)
{
    ["stash"] = null,
    ["weapon-case"] = "stash",
    ["nested-pouch"] = "weapon-case",
    ["pistol"] = "nested-pouch",
    ["mag-case"] = "stash",
    ["spare-magazine"] = "mag-case",
    ["equipment"] = null,
    ["equipped-pistol"] = "equipment"
};
Expect(InventoryAncestry.IsBelow("pistol", "stash", nestedInventory), "pistol in nested stash containers remains eligible");
Expect(InventoryAncestry.IsBelow("spare-magazine", "stash", nestedInventory), "spare magazine in a stash case remains eligible");
Expect(!InventoryAncestry.IsBelow("equipped-pistol", "stash", nestedInventory), "equipment branch is not mistaken for stash ownership");
Expect(!InventoryAncestry.IsBelow("missing", "stash", nestedInventory), "broken ancestry fails closed");
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
Expect(SafeSpawnSelector.TrySelect(
    spawnCandidates,
    null!,
    new WorldPoint(0, 0, 0),
    new WorldPoint(100, 0, 0),
    new[] { new WorldPoint(200, 0, 0) },
    spawnPolicy,
    42,
    out SpawnCandidate customSpawnRecovery), "custom spawn without native SpawnPoint remains recoverable");
Expect(customSpawnRecovery.Id is "safe-a" or "safe-b", "custom spawn still enforces corpse, killer and combat distance");

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
    new FakeMove(armament.SpareMagazineItemId, true, true, moved, rolledBack),
    () => true);
Expect(successfulTransfer.TryExecute(), "complete stash transfer commits");
Expect(moved.SequenceEqual(new[] { armament.PistolItemId, armament.SpareMagazineItemId }), "pistol tree and spare magazine move exactly once");
Expect(rolledBack.Count == 0, "successful stash transfer does not roll back");

moved.Clear();
rolledBack.Clear();
var failedTransfer = new ArmamentTransferTransaction(
    armament,
    new FakeMove(armament.PistolItemId, true, true, moved, rolledBack),
    new FakeMove(armament.SpareMagazineItemId, true, false, moved, rolledBack),
    () => true);
Expect(!failedTransfer.TryExecute(), "partial stash transfer fails closed");
Expect(moved.SequenceEqual(new[] { armament.PistolItemId }), "later stash move is not attempted after failure");
Expect(rolledBack.SequenceEqual(new[] { armament.SpareMagazineItemId, armament.PistolItemId }), "failed and completed moves roll back in reverse order");

moved.Clear();
rolledBack.Clear();
var failedPreflightTransfer = new ArmamentTransferTransaction(
    armament,
    new FakeMove(armament.PistolItemId, true, true, moved, rolledBack),
    new FakeMove(armament.SpareMagazineItemId, false, true, moved, rolledBack),
    () => true);
Expect(!failedPreflightTransfer.TryExecute(), "all stash moves preflight before mutation");
Expect(moved.Count == 0 && rolledBack.Count == 0, "failed transfer preflight is mutation-free");

moved.Clear();
rolledBack.Clear();
var ownershipChecks = 0;
var detachedInstalledMagazine = new ArmamentTransferTransaction(
    armament,
    new FakeMove(armament.PistolItemId, true, true, moved, rolledBack),
    new FakeMove(armament.SpareMagazineItemId, true, true, moved, rolledBack),
    () => ++ownershipChecks == 1);
Expect(!detachedInstalledMagazine.TryExecute(), "installed magazine must remain attached to the moved pistol tree");
Expect(rolledBack.SequenceEqual(new[] { armament.SpareMagazineItemId, armament.PistolItemId }), "ownership drift rolls both physical moves back");

Expect(RecoveryOwnershipPlan.TryCreate(
    "persistent-equipment-root",
    "persistent-equipment-root",
    armament,
    out RecoveryOwnershipPlan ownership), "distinct controller roots retain the persistent profile identity");
Expect(ownership.CorpseEquipmentRootId == ownership.RecoveryEquipmentRootId, "raid-end equipment pointer remains resolvable");
Expect(ownership.Armament == armament, "stash armament identities are preserved by ownership plan");
Expect(RecoveryOwnershipPlan.TryCreate(
    "same-equipment-root",
    "same-equipment-root",
    null,
    out _), "unarmed recovery retains the persistent root identity");
Expect(!RecoveryOwnershipPlan.TryCreate(
    "corpse-equipment-root",
    armament.PistolItemId,
    armament,
    out _), "recovery cannot replace the authoritative equipment identity");
Expect(RecoveryOwnershipPlan.TryCreate(
    "persistent-unarmed-root",
    "persistent-unarmed-root",
    null,
    out RecoveryOwnershipPlan unarmedOwnership), "unarmed recovery still receives a distinct empty equipment object");
Expect(unarmedOwnership.Armament is null, "unarmed ownership plan generates no equipment");

var unavailableGate = new RecoveryFinalizationGate();
var oldOwnerPlayer = new object();
var replacementOwnerPlayer = new object();
Expect(OwnerHandoffGuard.MustPreserveReplacement(replacementOwnerPlayer, oldOwnerPlayer), "late old-owner cleanup preserves the replacement global player");
Expect(!OwnerHandoffGuard.MustPreserveReplacement(oldOwnerPlayer, oldOwnerPlayer), "active owner cleanup may clear its own global player");
Expect(!OwnerHandoffGuard.MustPreserveReplacement(null!, oldOwnerPlayer), "empty global player has nothing to preserve");
Expect(OwnerHandoffGuard.MustPreserveReplacement(replacementOwnerPlayer, null!), "owner destruction with no player cannot clear a live replacement");
Expect(unavailableGate.HandleDeathBoundary("gate-raid-native", "gate-corpse-native", executorReady: false) == NativeFinalizationDecision.ContinueNative, "unavailable executor continues native death");
Expect(unavailableGate.Snapshot.State == RecoveryState.FinalDeath, "unavailable executor closes the raid lifecycle");

var pendingGate = new RecoveryFinalizationGate();
Expect(pendingGate.HandleDeathBoundary("gate-raid", "gate-corpse", executorReady: true) == NativeFinalizationDecision.SuppressForRecovery, "ready executor suppresses first native finalization");
Expect(pendingGate.Snapshot.State == RecoveryState.RecoveryPending, "ready executor reserves recovery before asynchronous work");
Expect(pendingGate.HandleDeathBoundary("gate-raid", "gate-corpse", executorReady: true) == NativeFinalizationDecision.SuppressDuplicate, "duplicate pending callback stays suppressed");
Expect(pendingGate.Snapshot.State == RecoveryState.RecoveryPending, "duplicate callback cannot mutate pending recovery");
Expect(!pendingGate.ConfirmRecovery(""), "recovery cannot complete without loadout transaction identity");
Expect(pendingGate.ConfirmRecovery("unarmed-transaction"), "unarmed recovery transaction can complete explicitly");
Expect(pendingGate.Snapshot.RecoveryConsumed, "successful recovery consumes the raid life before gameplay resumes");
Expect(pendingGate.HandleDeathBoundary("gate-raid", "second-corpse", executorReady: true) == NativeFinalizationDecision.ContinueNative, "second death always continues native finalization");
Expect(pendingGate.Snapshot.State == RecoveryState.FinalDeath, "second death is terminal");
Expect(!pendingGate.ConfirmRecovery("second-loadout"), "second death cannot confirm another recovery");

var abortedGate = new RecoveryFinalizationGate();
Expect(abortedGate.HandleDeathBoundary("gate-abort", "gate-abort-corpse", executorReady: true) == NativeFinalizationDecision.SuppressForRecovery, "abort test enters pending recovery");
Expect(abortedGate.AbortPendingRecovery(), "asynchronous recovery failure resumes terminal native path");
Expect(abortedGate.Snapshot.State == RecoveryState.FinalDeath, "aborted recovery cannot remain pending");

Expect(PaidHealingPolicy.CalculateCost(100f, 10f, 100f, false, 0f, 0, 1, 0f) == 1000, "native health-point price is charged");
Expect(PaidHealingPolicy.CalculateCost(100f, 10f, 1f, false, 0f, 0, 1, 0f) == 50, "native loyalty coefficient is clamped to five percent");
Expect(PaidHealingPolicy.CalculateCost(100f, 10f, 100f, true, 0f, 0, 1, 0f) == 0, "native fast-heal trial remains free");
Expect(PaidHealingPolicy.CalculateCost(100f, 10f, 100f, false, 0.01f, 10, 4, 0.5f) == 951, "native charisma float precision and ceiling are preserved");
Expect(PaidHealingPolicy.PlanDebits(125, new[] { 50, 100 })!.SequenceEqual(new[] { 50, 75 }), "ruble debit spans stable stash stacks");
Expect(PaidHealingPolicy.PlanDebits(151, new[] { 50, 100 }) is null, "insufficient stash rubles reject recovery");
Expect(PaidHealingPolicy.PlanDebits(0, Array.Empty<int>())!.Count == 0, "free native healing requires no ruble stack");

var firstLifeStats = new LifeStatisticsSnapshot(1250, new[]
{
    new LifeKill("kill-a", "Scav A", "AKS-74U", "Head", 42f),
    new LifeKill("kill-b", "Scav B", "Glock 17", "Thorax", 18f)
});
var cumulativeStats = new LifeStatisticsSnapshot(2100, new[]
{
    new LifeKill("kill-a", "Scav A", "AKS-74U", "Head", 42f),
    new LifeKill("kill-b", "Scav B", "Glock 17", "Thorax", 18f),
    new LifeKill("kill-c", "PMC", "Glock 17", "Head", 31f)
});
LifeStatisticsReport separatedStats = LifeStatisticsReport.Separate(firstLifeStats, cumulativeStats);
Expect(separatedStats.FirstLife.Experience == 1250 && separatedStats.FirstLife.Kills.Count == 2, "first-life statistics remain intact");
Expect(separatedStats.SecondLife.Experience == 850 && separatedStats.SecondLife.Kills.Single().Identity == "kill-c", "cumulative native statistics are separated into second-life deltas");
var resetStats = new LifeStatisticsSnapshot(600, new[] { new LifeKill("kill-d", "Scav C", "Makarov", "Stomach", 9f) });
LifeStatisticsReport separatedResetStats = LifeStatisticsReport.Separate(firstLifeStats, resetStats);
Expect(separatedResetStats.SecondLife.Experience == 600 && separatedResetStats.SecondLife.Kills.Single().Identity == "kill-d", "reset replacement-player statistics remain a complete second-life snapshot");
var mixedStats = new LifeStatisticsSnapshot(600, cumulativeStats.Kills);
LifeStatisticsReport separatedMixedStats = LifeStatisticsReport.Separate(firstLifeStats, mixedStats);
Expect(separatedMixedStats.SecondLife.Experience == 600 && separatedMixedStats.SecondLife.Kills.Single().Identity == "kill-c", "kill and experience reset behavior are separated independently");

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
