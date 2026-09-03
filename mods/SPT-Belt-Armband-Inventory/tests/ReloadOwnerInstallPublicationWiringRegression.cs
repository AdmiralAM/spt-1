using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ReloadOwnerInstallPublicationWiringRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Reload owner install publication wiring regression failed: module root could not be resolved.");

        string gate = File.ReadAllText(Path.Combine(root, "src", "ReloadOwnerInstallPublicationGate.cs"));
        string fastAccess = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs"));

        string[] required =
        {
            "typeof(FastAccessSlotPatches).GetMethod(",
            "\"TryInstall\", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public",
            "nameof(FastAccessReloadRuntime.PromoteReachability)",
            "nameof(ReloadCandidateBridgeRuntime.EnterReloadScope)",
            "nameof(ReloadCandidateBridgeRuntime.AppendCandidates)",
            "Patch(owner, patch, harmonyMethodType, fastAccessInstall, installPrefix, null, installFinalizer);",
            "Patch(owner, patch, harmonyMethodType, promoteReachability, promotePrefix, null, null);",
            "Patch(owner, patch, harmonyMethodType, enterReload, enterPrefix, null, null);",
            "Patch(owner, patch, harmonyMethodType, appendCandidates, appendPrefix, null, null);",
            "BeginForRegression();",
            "EndForRegression();",
            "HasLiveReachabilityPublicationContract()",
            "HasLiveCandidatePublicationContract()",
            "FastAccessReloadRuntime.ItemType != null",
            "FastAccessReloadRuntime.MagazineType != null",
            "FastAccessReloadRuntime.GetAllParentItems != null",
            "FastAccessReloadRuntime.ReadTemplateId != null",
            "ReloadCandidateBridgeRuntime.GetItemsInSlots != null",
            "ReloadCandidateBridgeRuntime.BeltSlotsArgument != null",
            "ReloadCandidateBridgeRuntime.ReturnType != null",
            "ReloadCandidateBridgeRuntime.GetAllParentItems != null",
            "ReloadCandidateBridgeRuntime.ReadTemplateId != null",
            "if (HasLiveCandidatePublicationContract()) return true;",
            "__result = __2;",
            "return false;",
            "if (prefix != null) prefixAssigned = true;",
            "if (postfix != null) postfixAssigned = true;",
            "if (finalizer != null) finalizerAssigned = true;"
        };
        foreach (string token in required)
            if (!gate.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException("Reload owner install publication wiring regression failed: missing exact gate contract: " + token);

        string[] forbidden =
        {
            "prefixAssigned = prefix != null;",
            "postfixAssigned = postfix != null;",
            "finalizerAssigned = finalizer != null;"
        };
        foreach (string token in forbidden)
            if (gate.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException("Reload owner install publication wiring regression failed: optional Harmony slot may incorrectly revoke an already-satisfied null request: " + token);

        int installed = fastAccess.IndexOf("installed = true;", StringComparison.Ordinal);
        int reachability = fastAccess.IndexOf("bool reachability = TryInstallReloadReachability();", StringComparison.Ordinal);
        int candidate = fastAccess.IndexOf("bool candidateBridge = reachability && TryInstallReloadCandidateBridge", StringComparison.Ordinal);
        if (installed < 0 || reachability < 0 || candidate < 0 || !(installed < reachability && reachability < candidate))
            throw new InvalidOperationException("Reload owner install publication wiring regression failed: FastAccess owner publication order changed unexpectedly.");

        int resetReachability = fastAccess.IndexOf("FastAccessReloadRuntime.Reset();", StringComparison.Ordinal);
        int resetCandidate = fastAccess.IndexOf("ReloadCandidateBridgeRuntime.Reset();", StringComparison.Ordinal);
        if (resetReachability < 0 || resetCandidate < 0)
            throw new InvalidOperationException("Reload owner install publication wiring regression failed: stale-owner fencing requires existing runtime Reset on Harmony rollback/teardown paths.");

        if (fastAccess.Contains("TryInstallReloadCandidateBridge(inventoryType, slotEnumType, dedicatedBelt) ||", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload owner install publication wiring regression failed: candidate bridge must not gain alternate discovery/retry semantics.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "src", "ReloadOwnerInstallPublicationGate.cs");
            if (File.Exists(candidate)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "src", "ReloadOwnerInstallPublicationGate.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "src", "ReloadOwnerInstallPublicationGate.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }
}
