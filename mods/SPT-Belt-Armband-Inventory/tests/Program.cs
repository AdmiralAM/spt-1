using System;
using System.Collections.Generic;
using System.Linq;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Tests;

internal static class Program
{
    static int assertions;
    static readonly string[] Vanilla =
    {
        BeltSlotPlan.TacticalVest,
        BeltSlotPlan.Pockets,
        BeltSlotPlan.Backpack,
        BeltSlotPlan.SecuredContainer,
        BeltSlotPlan.Dogtag
    };

    static void Main()
    {
        Assert(PackNStrapCompatibility.IsClientPresent(new[] { "other", "com.wtt.packnstrap" }), "Pack 'n' Strap client GUID selects companion mode");
        Assert(PackNStrapCompatibility.IsClientPresent(new[] { "COM.WTT.PACKNSTRAP" }), "Pack 'n' Strap GUID detection is case-insensitive");
        Assert(!PackNStrapCompatibility.IsClientPresent(new[] { "com.trenchfoot.beltslot" }), "legacy BeltSlot does not impersonate Pack 'n' Strap");
        Assert(PackNStrapCompatibility.IsServerPresent(new[] { "System", "WTT-PackNStrapServer" }), "Pack 'n' Strap server assembly selects companion mode");
        Assert(!PackNStrapCompatibility.IsServerPresent(new[] { "SPT.Server", "SPT-Belt-Armband-Inventory.Server" }), "B&A server alone stays in standalone mode");
        Assert(SecureContainerCompatibilityPolicy.GammaTemplateIds.Contains("665ee77ccf2d642e98220bca"), "the equipped SPT Gamma template is an explicit compatibility host");
        Assert(SecureContainerCompatibilityPolicy.IsSupportedPackNStrapContainer("669c10fa06c00c483c58537a", new[] { SecureContainerCompatibilityPolicy.PackNStrapContainerParent }), "Pack 'n' Strap cash pouch is admitted through its owned parent");
        Assert(SecureContainerCompatibilityPolicy.IsSupportedPackNStrapContainer(SecureContainerCompatibilityPolicy.PackNStrapPlateContainer, Array.Empty<string>()), "Pack 'n' Strap plate case is admitted by exact identity");
        Assert(!SecureContainerCompatibilityPolicy.IsSupportedPackNStrapContainer("5c093e3486f77430cb02e593", new[] { "5795f317245977243854e041" }), "unrelated vanilla simple containers remain forbidden");
        Assert(TgcCompatibilityPolicy.BeltTemplateIds.Count == 5, "TGC 3.0.0 belt integration is an exact five-template allowlist");
        Assert(TgcCompatibilityPolicy.IsBelt("672e2e75a16c1d2034c384cf"), "TGC combat belt is assigned to the B&A Belt host");
        Assert(!TgcCompatibilityPolicy.IsBelt(TgcCompatibilityPolicy.ToolBoxTemplateId), "TGC Tool Box cannot impersonate a Belt");
        Assert(TgcCompatibilityPolicy.IsExplicitSecureContainerPouch("672e2e758808bacbb9d5abc4"), "TGC Ammo Pouch is explicitly admitted to supported secure containers");
        Assert(TgcCompatibilityPolicy.IsExplicitSecureContainerPouch("672e2e7526ba61dbb88be7ff"), "TGC First Aid container is explicitly admitted to supported secure containers");
        Assert(!TgcCompatibilityPolicy.IsExplicitSecureContainerPouch(TgcCompatibilityPolicy.ToolBoxTemplateId), "TGC Tool Box remains excluded from secure containers");
        Assert(TgcCompatibilityPolicy.IntegrationSourcePr == 362
            && TgcCompatibilityPolicy.IntegrationContractHead == "bd1500b86c356f5e97fade75cf0c1df974ae9621",
            "B&A TGC ownership contract is pinned to PR #362 exact reviewed head");
        var absentArmBand = new HashSet<string>();
        var absentBelt = new HashSet<string>();
        Assert(TgcCompatibilityPolicy.TryNormalizeBeltHostFilters(absentArmBand, absentBelt, Array.Empty<string>(), out int absentChanges)
            && absentChanges == 0 && absentArmBand.Count == 0 && absentBelt.Count == 0,
            "missing TGC is a fail-closed no-op");
        var stockArmBand = new HashSet<string>(TgcCompatibilityPolicy.BeltTemplateIds);
        var ownedBelt = new HashSet<string>();
        Assert(TgcCompatibilityPolicy.TryNormalizeBeltHostFilters(stockArmBand, ownedBelt, TgcCompatibilityPolicy.BeltTemplateIds, out int firstBeltChanges)
            && firstBeltChanges == 10 && stockArmBand.Count == 0 && ownedBelt.SetEquals(TgcCompatibilityPolicy.BeltTemplateIds),
            "stock TGC ArmBand mutations are transferred to exact B&A slot15 ownership");
        Assert(TgcCompatibilityPolicy.TryNormalizeBeltHostFilters(stockArmBand, ownedBelt, TgcCompatibilityPolicy.BeltTemplateIds, out int secondBeltChanges)
            && secondBeltChanges == 0,
            "TGC Belt filter ownership is idempotent");
        var secureFilter = new HashSet<string>(TgcCompatibilityPolicy.SecureContainerPouchAllowlist) { TgcCompatibilityPolicy.ToolBoxTemplateId };
        string[] allTgcContainers = TgcCompatibilityPolicy.SecureContainerPouchAllowlist.Concat(new[] { TgcCompatibilityPolicy.ToolBoxTemplateId }).ToArray();
        Assert(TgcCompatibilityPolicy.TryNormalizeSecureContainerFilter(secureFilter, allTgcContainers, true, out int firstSecureChanges),
            "complete stock TGC secure family is accepted");
        Assert(firstSecureChanges > 0,
            "stock TGC secure normalization reports its owned replacement mutations");
        Assert(secureFilter.SetEquals(TgcCompatibilityPolicy.SecureContainerPouchAllowlist),
            $"stock TGC secure normalization leaves exact two-item Gamma allowlist (actual={string.Join(',', secureFilter)})");
        Assert(TgcCompatibilityPolicy.TryNormalizeSecureContainerFilter(secureFilter, allTgcContainers, true, out int secondSecureChanges)
            && secondSecureChanges == 0 && secureFilter.SetEquals(TgcCompatibilityPolicy.SecureContainerPouchAllowlist),
            "TGC secure filter normalization is content-idempotent");
        Assert(!TgcCompatibilityPolicy.TryNormalizeBeltHostFilters(new HashSet<string>(), new HashSet<string>(), new[] { TgcCompatibilityPolicy.BeltTemplateIds.First() }, out _),
            "partial TGC Belt publication fails closed before mutation");
        foreach (string tgcBelt in TgcCompatibilityPolicy.BeltTemplateIds)
        {
            Assert(WearableItemDescriptorRegistry.HasCapability(tgcBelt, AccessoryCapability.FastAccess), "TGC Belt receives exact slot15 fast access");
            Assert(!WearableItemDescriptorRegistry.HasCapability(tgcBelt, AccessoryCapability.DeathRetention), "foreign TGC Belt never receives Admiral death protection");
        }
        LocalPackNStrapImportRegression.Run();
        UseItemsAnywhereCompatibilityRegression.Run();
        BeltAccessApiRegression.Run();
        ArmBandVariantCatalogRegression.Run();
        ArmBandLootPolicyRegression.Run();
        ArmBandFeatureConfigRegression.Run();
        HeadBandVisualAssetRegression.Run();
        LegacyCashBoxProfileMigrationRegression.Run();
        TgcBeltProfileMigrationRegression.Run();
        SPTBeltArmbandInventory.Tests.ProfileCleanupRegression.Run();
        SPTBeltArmbandInventory.Tests.DedicatedWearableSlotContractRegression.Run();
        SPTBeltArmbandInventory.Tests.DedicatedSlotPresentationPolicyRegression.Run();
        ReloadScopeThreadIsolationRegression.Run();
        ReloadScopeEpochRegression.Run();
        ReloadSlotArrayContentPinRegression.Run();
        Assert(BeltSlotPlan.IsExpectedContainerPanelOrder(Vanilla), "recognizes SPT 4.1 container order");
        Assert(!BeltSlotPlan.IsExpectedContainerPanelOrder(new[] { BeltSlotPlan.Pockets }), "rejects unrelated enum arrays");

        string[] above = BeltSlotPlan.Build(Vanilla, BeltSlotPosition.AbovePockets, true);
        Assert(Array.IndexOf(above, BeltSlotPlan.ArmBand) + 1 == Array.IndexOf(above, BeltSlotPlan.Pockets), "legacy layout helper places ArmBand above pockets");
        Assert(above.Length == Vanilla.Length + 1, "legacy layout helper adds exactly one ArmBand row");

        string[] below = BeltSlotPlan.Build(Vanilla, BeltSlotPosition.BelowPockets, true);
        Assert(Array.IndexOf(below, BeltSlotPlan.ArmBand) == Array.IndexOf(below, BeltSlotPlan.Pockets) + 1, "legacy layout helper places ArmBand below pockets");
        Assert(below.Length == Vanilla.Length + 1, "legacy below layout adds exactly one row");

        string[] duplicateInput = Vanilla.Concat(new[] { BeltSlotPlan.ArmBand, BeltSlotPlan.ArmBand }).ToArray();
        string[] normalized = BeltSlotPlan.Build(duplicateInput, BeltSlotPosition.BelowPockets, true);
        Assert(normalized.Count(x => x == BeltSlotPlan.ArmBand) == 1, "legacy layout helper is idempotent");

        string[] hidden = BeltSlotPlan.Build(duplicateInput, BeltSlotPosition.AbovePockets, false);
        Assert(!hidden.Contains(BeltSlotPlan.ArmBand), "legacy hidden layout removes duplicate ArmBand row");
        Assert(hidden.SequenceEqual(Vanilla), "legacy hidden layout preserves vanilla order");

        string[] dedicatedEquipmentOrder = { "TacticalVest", "Pockets", "Backpack", "Headwear" };
        string[] withBelt = InsertDedicated(dedicatedEquipmentOrder, DedicatedWearableSlotContract.Belt);
        Assert(Array.IndexOf(withBelt, DedicatedWearableSlotContract.BeltSlotId) == Array.IndexOf(withBelt, "Pockets") + 1,
            "dedicated Belt is anchored immediately after Pockets");
        Assert(Array.IndexOf(withBelt, DedicatedWearableSlotContract.BeltSlotId) + 1 == Array.IndexOf(withBelt, "Backpack"),
            "dedicated Belt remains between Pockets and Backpack");
        string[] withHeadBand = InsertDedicated(withBelt, DedicatedWearableSlotContract.HeadBand);
        Assert(Array.IndexOf(withHeadBand, DedicatedWearableSlotContract.HeadBandSlotId) + 1 == Array.IndexOf(withHeadBand, "Headwear"),
            "dedicated HeadBand is anchored immediately before Headwear");

        Assert(!BeltSlotPlan.ShouldExposeBelt(false, false), "empty slot never exposes legacy panel projection");
        Assert(!BeltSlotPlan.ShouldExposeBelt(true, false), "plain armband never exposes legacy panel projection");
        Assert(!BeltSlotPlan.ShouldExposeBelt(false, true), "container flag alone is insufficient");
        Assert(!BeltSlotPlan.ShouldExposeBelt(true, true), "native dedicated-slot path keeps legacy ArmBand panel projection disabled");

        Assert(AccessoryCategoryPolicy.IsSupported(AccessoryCategory.ArmBand), "ArmBand category is supported");
        Assert(AccessoryCategoryPolicy.IsSupported(AccessoryCategory.Belt), "Belt category is supported");
        Assert(AccessoryCategoryPolicy.IsSupported(AccessoryCategory.HeadBand), "HeadBand category is supported");
        Assert(AccessoryCategoryPolicy.Capacity(AccessoryCategory.HeadBand) == AccessoryCapacityBand.Micro, "HeadBand uses micro capacity band");
        Assert(AccessoryCategoryPolicy.Capacity(AccessoryCategory.ArmBand) == AccessoryCapacityBand.Compact, "ArmBand uses compact capacity band");
        Assert(AccessoryCategoryPolicy.Capacity(AccessoryCategory.Belt) == AccessoryCapacityBand.Expanded, "Belt uses expanded capacity band");
        Assert(AccessoryCategoryPolicy.HostState(AccessoryCategory.ArmBand) == AccessoryHostState.Validated, "ArmBand host remains runtime validated");
        Assert(AccessoryCategoryPolicy.HostState(AccessoryCategory.Belt) == AccessoryHostState.Validated, "dedicated Belt host reflects physical slot/filter validation");
        Assert(AccessoryCategoryPolicy.HostState(AccessoryCategory.HeadBand) == AccessoryHostState.RuntimeCandidate, "HeadBand remains runtime-candidate until the batched physical gate passes");
        Assert(!AccessoryCategoryPolicy.CanExposeContainer(AccessoryCategory.Belt, false, true), "category alone cannot expose an empty host");
        Assert(AccessoryCategoryPolicy.CanExposeContainer(AccessoryCategory.HeadBand, true, true), "container-capable HeadBand may expose its dedicated host");
        Assert(AccessoryCategoryPolicy.CanActivateRuntime(AccessoryCategory.ArmBand, true, true), "validated ArmBand container can activate its runtime route");
        Assert(AccessoryCategoryPolicy.CanActivateRuntime(AccessoryCategory.Belt, true, true), "physically validated Belt host can activate its dedicated runtime route");
        Assert(!AccessoryCategoryPolicy.CanActivateRuntime(AccessoryCategory.HeadBand, true, true), "HeadBand does not become validated merely because a candidate implementation exists");
        Assert(!AccessoryCategoryPolicy.CanExposeContainer((AccessoryCategory)99, true, true), "unknown category fails closed");
        Assert(!AccessoryCategoryPolicy.CanActivateRuntime((AccessoryCategory)99, true, true), "unknown category cannot activate runtime behavior");

        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.PanelProjection), "native GridWindow ArmBand path does not own legacy panel projection");
        Assert(AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.FastAccess), "magazine ArmBand candidate retains reachable-container fast access");
        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.PaymentSource), "magazine-only ArmBand candidate does not install payment-source behavior");
        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.GrenadeAccess), "magazine-only ArmBand candidate does not install grenade behavior");
        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.Belt, AccessoryCapability.PanelProjection), "dedicated Belt presentation is slot-owned rather than a generic capability");
        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.HeadBand, AccessoryCapability.FastAccess), "HeadBand candidate does not inherit Belt fast-access behavior");
        Assert(!AccessoryCapabilityPolicy.Has((AccessoryCategory)99, AccessoryCapability.PanelProjection), "unknown category has no capabilities");
        Assert(!AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.None), "empty capability request fails closed");
        Assert(!AccessoryCapabilityPolicy.CanUse(AccessoryCategory.ArmBand, AccessoryCapability.GrenadeAccess, true, true), "disabled grenade capability cannot activate on the magazine candidate");
        Assert(AccessoryCapabilityPolicy.CanUse(AccessoryCategory.ArmBand, AccessoryCapability.FastAccess, true, true), "assigned fast-access capability activates only for a real container");
        Assert(!AccessoryCapabilityPolicy.CanUse(AccessoryCategory.ArmBand, AccessoryCapability.FastAccess, true, false), "fast-access capability still requires a container item");
        Assert(WearableItemDescriptorRegistry.HasCapability(RuntimeIdentity.CandidateItemId, AccessoryCapability.FastAccess),
            "Magazine Armband is an exact fast-access/reload root");
        Assert(WearableItemDescriptorRegistry.HasCapability(RuntimeIdentity.DedicatedMagazineBeltItemId, AccessoryCapability.FastAccess),
            "Magazine Belt is an exact fast-access/reload root");
        Assert(!WearableItemDescriptorRegistry.HasCapability(RuntimeIdentity.WristWalletItemId, AccessoryCapability.FastAccess),
            "Wrist Wallet is not a reload root");
        Assert(!WearableItemDescriptorRegistry.HasCapability(RuntimeIdentity.EmergencyHeadBandItemId, AccessoryCapability.FastAccess),
            "Utility HeadBand is not a reload root");
        Assert(!FastAccessSlotPolicy.ShouldPromoteReloadReachability(true, true, true),
            "vanilla reachable magazines keep vanilla result/order and are never promoted by B&A&HB");
        Assert(FastAccessSlotPolicy.ShouldPromoteReloadReachability(false, true, true),
            "unreachable magazine under an exact fast-access wearable may become a fallback reload source");
        Assert(!FastAccessSlotPolicy.ShouldPromoteReloadReachability(false, false, true),
            "non-magazine descendants are never promoted into reload reachability");
        Assert(!FastAccessSlotPolicy.ShouldPromoteReloadReachability(false, true, false),
            "magazines outside exact B&A&HB fast-access wearable roots remain vanilla-unreachable");
        string[] vanillaReloadSlots = { BeltSlotPlan.TacticalVest, BeltSlotPlan.Pockets };
        string[] extendedReloadSlots = FastAccessSlotPolicy.Extend(vanillaReloadSlots);
        Assert(extendedReloadSlots.Take(vanillaReloadSlots.Length).SequenceEqual(vanillaReloadSlots),
            "B&A&HB preserves the complete vanilla reload-slot order as the priority prefix");
        Assert(extendedReloadSlots.Skip(vanillaReloadSlots.Length).SequenceEqual(new[] { BeltSlotPlan.ArmBand, RuntimeIdentity.DedicatedBeltWireSlotId }),
            "Magazine Armband and Magazine Belt are appended only after vanilla reload slots");

        RunOwnedArrayRollbackRegression();

        Assert(ScavBeltPolicy.ShouldRestore(RuntimeIdentity.CandidateItemId, true, true),
            "ArmBand runtime candidate survives Scav ReplaceInventory when deleted");
        Assert(ScavBeltPolicy.ShouldRestore(RuntimeIdentity.DedicatedMagazineBeltItemId, true, true),
            "dedicated Magazine Belt survives Scav ReplaceInventory when deleted");
        Assert(ScavBeltPolicy.ShouldRestore(RuntimeIdentity.EmergencyHeadBandItemId, true, true),
            "Emergency HeadBand survives Scav ReplaceInventory when deleted");
        Assert(!ScavBeltPolicy.ShouldRestore(RuntimeIdentity.WristWalletItemId, true, true),
            "Wrist Wallet without explicit Scav capability is not silently restored");
        Assert(!ScavBeltPolicy.ShouldRestore(RuntimeIdentity.DedicatedMagazineBeltItemId, false, true),
            "Scav lifecycle does not mutate an already-active Belt slot");
        Assert(!ScavBeltPolicy.ShouldRestore(RuntimeIdentity.DedicatedMagazineBeltItemId, true, false),
            "Scav lifecycle requires a real container item");

        Console.WriteLine("SPT Belt/Armband Inventory profile safety, dedicated slot and lifecycle regressions passed.");
    }

    static void RunOwnedArrayRollbackRegression()
    {
        int[] original = { 1, 2 };
        int[] installed = { 1, 2, 15 };
        Array installedSnapshot = FastAccessSlotPolicy.CaptureArrayContentSnapshot(installed);
        object current = installed;
        bool released;
        bool clean = FastAccessSlotPolicy.TryRestoreOwnedReference(true, () => current, value => current = value, original, installed, installedSnapshot, out released);
        Assert(clean && released && ReferenceEquals(current, original),
            "exact-owned fast-access array restore is proven only after installed content and original reference read-back are proven");

        int[] foreign = { 9 };
        current = foreign;
        int foreignWrites = 0;
        bool foreignSafe = FastAccessSlotPolicy.TryRestoreOwnedReference(true, () => current, value => { foreignWrites++; current = value; }, original, installed, installedSnapshot, out released);
        Assert(foreignSafe && released && foreignWrites == 0 && ReferenceEquals(current, foreign),
            "foreign replacement is preserved as an ownership-released no-op");

        current = installed;
        bool failedWrite = FastAccessSlotPolicy.TryRestoreOwnedReference(true, () => current, value => throw new InvalidOperationException("restore blocked"), original, installed, installedSnapshot, out released);
        Assert(!failedWrite && !released && ReferenceEquals(current, installed),
            "restore failure while the exact installed ref+content is live retains rollback authority and fails closed");

        bool failedRead = FastAccessSlotPolicy.TryRestoreOwnedReference(true, () => throw new InvalidOperationException("read blocked"), value => { }, original, installed, installedSnapshot, out released);
        Assert(!failedRead && !released,
            "unreadable owned-array state is ambiguous and cannot be treated as a successful rollback");
    }

    static string[] InsertDedicated(string[] source, DedicatedWearableSlotDescriptor descriptor)
    {
        int anchor = Array.IndexOf(source, descriptor.UiAnchor);
        if (anchor < 0) throw new InvalidOperationException("Missing UI anchor " + descriptor.UiAnchor);
        int insertAt = descriptor.InsertAfterAnchor ? anchor + 1 : anchor;
        var result = new string[source.Length + 1];
        Array.Copy(source, 0, result, 0, insertAt);
        result[insertAt] = descriptor.SlotId;
        Array.Copy(source, insertAt, result, insertAt + 1, source.Length - insertAt);
        return result;
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
        assertions++;
    }
}
