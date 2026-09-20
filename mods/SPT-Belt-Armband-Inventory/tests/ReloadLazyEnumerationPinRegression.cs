using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadLazyEnumerationPinRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var belt = new FakeItem(RuntimeIdentity.DedicatedMagazineBeltItemId);
        var magazine = new FakeMagazine("belt-magazine", belt);
        IEnumerable<FakeItem> vanilla = new FakeItem[] { new FakeMagazine("vanilla-magazine") };

        int[] originalFast = { 1, 2, 3 };
        int[] installedFast = { 1, 2, 3, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        int[] originalBind = { 4, 5, 6 };
        int[] installedBind = { 4, 5, 6, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };

        Configure(originalFast, installedFast, originalBind, installedBind);
        var beltArgument = (int[])ReloadCandidateBridgeRuntime.BeltSlotsArgument;
        MethodInfo exactMethod = RequireMethod(nameof(FakeInventory.GetItemsInSlots));
        MethodInfo alternateMethod = RequireMethod(nameof(FakeInventory.GetItemsInSlotsAlternate));

        var preQueryInventory = new FakeInventory(magazine, null);
        IEnumerable<FakeItem> driftingVanilla = EnumerateVanillaWithDrift(
            new FakeMagazine("vanilla-drift-magazine"),
            () => originalFast[1] = 98);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object preQueryDrifted = ReloadCandidateBridgeRuntime.AppendCandidates(preQueryInventory, originalFast, driftingVanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(preQueryDrifted, driftingVanilla),
            "same-reference slot-array drift during lazy vanilla enumeration must preserve the exact vanilla enumerable object");
        Require(preQueryInventory.QueryCount == 0,
            "slot-array drift during vanilla enumeration must fail closed before the single pseudo-slot15 query");

        originalFast[1] = 2;
        Require(ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalFast),
            "restoring exact content after vanilla-enumeration drift must restore the recognized retained-array pin");

        var preQueryArgumentInventory = new FakeInventory(magazine, null);
        IEnumerable<FakeItem> driftingArgumentVanilla = EnumerateVanillaWithDrift(
            new FakeMagazine("vanilla-pseudo-slot-drift-magazine"),
            () => beltArgument[0] = RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object preQueryArgumentDrifted = ReloadCandidateBridgeRuntime.AppendCandidates(preQueryArgumentInventory, originalFast, driftingArgumentVanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(preQueryArgumentDrifted, driftingArgumentVanilla),
            "same-reference pseudo-slot argument drift during lazy vanilla enumeration must preserve exact vanilla identity");
        Require(preQueryArgumentInventory.QueryCount == 0,
            "pseudo-slot argument drift during vanilla enumeration must fail closed before any fallback query");
        beltArgument[0] = RuntimeIdentity.DedicatedBeltEquipmentSlotValue;

        var preQueryMethodInventory = new FakeInventory(magazine, null);
        IEnumerable<FakeItem> driftingMethodVanilla = EnumerateVanillaWithDrift(
            new FakeMagazine("vanilla-method-drift-magazine"),
            () => ReloadCandidateBridgeRuntime.GetItemsInSlots = alternateMethod);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object preQueryMethodDrifted = ReloadCandidateBridgeRuntime.AppendCandidates(preQueryMethodInventory, originalFast, driftingMethodVanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(preQueryMethodDrifted, driftingMethodVanilla),
            "MethodInfo replacement during lazy vanilla enumeration must preserve exact vanilla identity");
        Require(preQueryMethodInventory.QueryCount == 0 && preQueryMethodInventory.AlternateQueryCount == 0,
            "MethodInfo replacement during vanilla enumeration must fail closed before any fallback query");
        ReloadCandidateBridgeRuntime.GetItemsInSlots = exactMethod;

        var driftingInventory = new FakeInventory(magazine, () => originalFast[1] = 99);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object drifted = ReloadCandidateBridgeRuntime.AppendCandidates(driftingInventory, originalFast, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(drifted, vanilla),
            "same-reference slot-array drift during lazy Belt enumeration must preserve the exact vanilla enumerable object");
        Require(driftingInventory.QueryCount == 1,
            "lazy Belt slot-array drift must occur inside the one bounded pseudo-slot15 query rather than triggering a retry");

        originalFast[1] = 2;
        Require(ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalFast),
            "restoring the exact captured content must restore the recognized retained-array pin");

        var driftingArgumentInventory = new FakeInventory(
            magazine,
            () => beltArgument[0] = RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object argumentDrifted = ReloadCandidateBridgeRuntime.AppendCandidates(driftingArgumentInventory, originalFast, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(argumentDrifted, vanilla),
            "same-reference pseudo-slot argument drift during lazy Belt enumeration must preserve exact vanilla identity");
        Require(driftingArgumentInventory.QueryCount == 1,
            "lazy Belt pseudo-slot drift must not trigger a retry or second query");
        beltArgument[0] = RuntimeIdentity.DedicatedBeltEquipmentSlotValue;

        var driftingMethodInventory = new FakeInventory(
            magazine,
            () => ReloadCandidateBridgeRuntime.GetItemsInSlots = alternateMethod);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object methodDrifted = ReloadCandidateBridgeRuntime.AppendCandidates(driftingMethodInventory, originalFast, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(methodDrifted, vanilla),
            "MethodInfo replacement during lazy Belt enumeration must preserve exact vanilla identity");
        Require(driftingMethodInventory.QueryCount == 1 && driftingMethodInventory.AlternateQueryCount == 0,
            "lazy Belt MethodInfo drift must retain the captured one-query boundary and never redirect or retry");
        ReloadCandidateBridgeRuntime.GetItemsInSlots = exactMethod;

        ReloadEpochPublicationFence.ResetForRegression();
        var resetDuringVanillaInventory = new FakeInventory(magazine, null);
        IEnumerable<FakeItem> resettingVanilla = EnumerateVanillaWithDrift(
            new FakeMagazine("vanilla-reset-magazine"),
            () =>
            {
                ReloadCandidateBridgeRuntime.Reset();
                ReloadEpochPublicationFence.InvalidateForRegression();
            });
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object resetDuringVanilla = ReloadCandidateBridgeRuntime.AppendCandidates(resetDuringVanillaInventory, originalFast, resettingVanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(resetDuringVanilla, resettingVanilla),
            "teardown/reset during lazy vanilla enumeration must preserve the exact incoming vanilla enumerable object");
        Require(resetDuringVanillaInventory.QueryCount == 0 && resetDuringVanillaInventory.AlternateQueryCount == 0,
            "teardown/reset during vanilla enumeration must fail closed before pseudo-slot15 query and must not redirect or retry");

        Configure(originalFast, installedFast, originalBind, installedBind);
        ReloadEpochPublicationFence.ResetForRegression();
        var resetDuringBeltInventory = new FakeInventory(
            magazine,
            () =>
            {
                ReloadCandidateBridgeRuntime.Reset();
                ReloadEpochPublicationFence.InvalidateForRegression();
            });
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object resetDuringBelt = ReloadCandidateBridgeRuntime.AppendCandidates(resetDuringBeltInventory, originalFast, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Require(ReferenceEquals(resetDuringBelt, vanilla),
            "teardown/reset during lazy Belt enumeration must preserve the exact incoming vanilla enumerable object");
        Require(resetDuringBeltInventory.QueryCount == 1 && resetDuringBeltInventory.AlternateQueryCount == 0,
            "teardown/reset during Belt enumeration may consume only the already-entered single pseudo-slot15 query and must never retry or redirect");

        Configure(originalFast, installedFast, originalBind, installedBind);
        var healthyInventory = new FakeInventory(magazine, null);
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object healthy = ReloadCandidateBridgeRuntime.AppendCandidates(healthyInventory, originalFast, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        FakeItem[] merged = ((IEnumerable<FakeItem>)healthy).ToArray();
        Require(!ReferenceEquals(healthy, vanilla),
            "healthy lazy Belt enumeration with unchanged pinned inputs must publish a replacement sequence");
        Require(healthyInventory.QueryCount == 1,
            "healthy Belt fallback must perform exactly one pseudo-slot15 query");
        Require(merged.Length == 2 && ReferenceEquals(merged[1], magazine),
            "healthy lazy Belt enumeration must preserve vanilla prefix and append the exact Belt descendant");

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadEpochPublicationFence.ResetForRegression();
    }

    private static IEnumerable<FakeItem> EnumerateVanillaWithDrift(FakeItem item, Action duringEnumeration)
    {
        duringEnumeration();
        yield return item;
    }

    private static MethodInfo RequireMethod(string name)
    {
        return typeof(FakeInventory).GetMethod(name)
            ?? throw new InvalidOperationException("Reload lazy enumeration pin regression failed: fake method missing: " + name);
    }

    private static void Configure(object originalFast, object installedFast, object originalBind, object installedBind)
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = RequireMethod(nameof(FakeInventory.GetItemsInSlots));
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = originalFast;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = installedFast;
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBind;
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBind;
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        ReloadCandidateBridgeRuntime.GetAllParentItems = item => ((FakeItem)item).Parents;
        ReloadCandidateBridgeRuntime.ReadTemplateId = item => ((FakeItem)item).TemplateId;
        ReloadCandidateBridgeRuntime.LogWarning = message => throw new InvalidOperationException(
            "Reload lazy enumeration pin regression failed closed unexpectedly: " + message);
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
    }

    private sealed class FakeInventory
    {
        private readonly FakeItem item;
        private readonly Action? duringEnumeration;

        internal FakeInventory(FakeItem item, Action? duringEnumeration)
        {
            this.item = item;
            this.duringEnumeration = duringEnumeration;
        }

        internal int QueryCount { get; private set; }
        internal int AlternateQueryCount { get; private set; }

        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<int> slots)
        {
            QueryCount++;
            return Enumerate();
        }

        public IEnumerable<FakeItem> GetItemsInSlotsAlternate(IEnumerable<int> slots)
        {
            AlternateQueryCount++;
            return Enumerate();
        }

        private IEnumerable<FakeItem> Enumerate()
        {
            duringEnumeration?.Invoke();
            yield return item;
        }
    }

    private class FakeItem
    {
        internal string TemplateId { get; }
        internal IEnumerable Parents { get; }

        internal FakeItem(string templateId, params FakeItem[] parents)
        {
            TemplateId = templateId;
            Parents = parents ?? Array.Empty<FakeItem>();
        }
    }

    private sealed class FakeMagazine : FakeItem
    {
        internal FakeMagazine(string templateId, params FakeItem[] parents) : base(templateId, parents) { }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Reload lazy enumeration pin regression failed: " + message + ".");
    }
}
