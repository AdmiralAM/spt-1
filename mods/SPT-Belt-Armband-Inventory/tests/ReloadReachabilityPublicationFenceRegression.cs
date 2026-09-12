using System;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadReachabilityPublicationFenceRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        RequireStructuralContract();

        Func<object, IEnumerable> parents = _ => Array.Empty<object>();
        Func<object, string> reader = _ => null;
        Func<object, string> replacementReader = _ => null;

        try
        {
            FastAccessReloadRuntime.ItemType = typeof(object);
            FastAccessReloadRuntime.MagazineType = typeof(string);
            FastAccessReloadRuntime.GetAllParentItems = parents;
            FastAccessReloadRuntime.ReadTemplateId = reader;
            ReloadReachabilityPublicationFence.ResetForRegression();

            ReloadReachabilityPublicationFence.Snapshot healthy =
                ReloadReachabilityPublicationFence.CaptureForRegression(false);
            if (!ReloadReachabilityPublicationFence.ShouldPublishForRegression(healthy)
                || !ReloadReachabilityPublicationFence.SelectForRegression(true, healthy))
                throw new InvalidOperationException("Reload reachability publication fence regression failed: a healthy captured execution contract must permit false -> true promotion.");

            FastAccessReloadRuntime.ReadTemplateId = replacementReader;
            if (ReloadReachabilityPublicationFence.ShouldPublishForRegression(healthy)
                || ReloadReachabilityPublicationFence.SelectForRegression(true, healthy))
                throw new InvalidOperationException("Reload reachability publication fence regression failed: persistent execution-reference drift must restore the captured vanilla false result.");

            FastAccessReloadRuntime.ReadTemplateId = reader;
            if (!ReloadReachabilityPublicationFence.ShouldPublishForRegression(healthy))
                throw new InvalidOperationException("Reload reachability publication fence regression failed: exact reference restoration without a lifecycle transition unexpectedly invalidated the identity snapshot.");

            ReloadReachabilityPublicationFence.InvalidateForRegression();
            if (ReloadReachabilityPublicationFence.ShouldPublishForRegression(healthy)
                || ReloadReachabilityPublicationFence.SelectForRegression(true, healthy))
                throw new InvalidOperationException("Reload reachability publication fence regression failed: lifecycle generation drift must prevent stale false -> true promotion even after exact execution references are restored.");

            ReloadReachabilityPublicationFence.Snapshot fresh =
                ReloadReachabilityPublicationFence.CaptureForRegression(false);
            if (!ReloadReachabilityPublicationFence.ShouldPublishForRegression(fresh)
                || !ReloadReachabilityPublicationFence.SelectForRegression(true, fresh))
                throw new InvalidOperationException("Reload reachability publication fence regression failed: a fresh post-transition transaction must recover healthy promotion.");

            ReloadReachabilityPublicationFence.Snapshot vanillaTrue =
                ReloadReachabilityPublicationFence.CaptureForRegression(true);
            ReloadReachabilityPublicationFence.InvalidateForRegression();
            if (!ReloadReachabilityPublicationFence.SelectForRegression(false, vanillaTrue))
                throw new InvalidOperationException("Reload reachability publication fence regression failed: fail-closed publication must preserve an incoming vanilla true result rather than demoting it.");

            if (ReloadReachabilityPublicationFence.ShouldKeepAssemblyLoadSubscriptionForRegression(true, false)
                || ReloadReachabilityPublicationFence.ShouldKeepAssemblyLoadSubscriptionForRegression(true, true)
                || ReloadReachabilityPublicationFence.ShouldKeepAssemblyLoadSubscriptionForRegression(false, true)
                || !ReloadReachabilityPublicationFence.ShouldKeepAssemblyLoadSubscriptionForRegression(false, false))
                throw new InvalidOperationException("Reload reachability publication fence regression failed: post-subscription bootstrap retry truth-table drifted.");
        }
        finally
        {
            ReloadReachabilityPublicationFence.ResetForRegression();
            FastAccessReloadRuntime.Reset();
        }
    }

    private static void RequireStructuralContract()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Reload reachability publication fence regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "src", "ReloadReachabilityPublicationFence.cs"));
        string[] required =
        {
            "nameof(FastAccessReloadRuntime.PromoteReachability)",
            "nameof(FastAccessReloadRuntime.Reset)",
            "\"TryInstall\"",
            "new[] { typeof(object), typeof(bool).MakeByRefType() }",
            "install.ReturnType != typeof(bool)",
            "AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;",
            "bool retrySucceeded = TryInstall();",
            "ShouldKeepAssemblyLoadSubscriptionForRegression(retrySucceeded, terminalFailure)",
            "patch.Invoke(owner, promoteArgs);",
            "patch.Invoke(owner, resetArgs);",
            "patch.Invoke(owner, installArgs);",
            "bool rolledBack = TryRollback(owner, rollback);",
            "terminalFailure = owner != null && !rolledBack;",
            "__state = CaptureForRegression(result);",
            "result = __state.VanillaResult;",
            "Interlocked.Increment(ref generation);",
            "ReferenceEquals(ItemType, FastAccessReloadRuntime.ItemType)",
            "ReferenceEquals(MagazineType, FastAccessReloadRuntime.MagazineType)",
            "ReferenceEquals(GetAllParentItems, FastAccessReloadRuntime.GetAllParentItems)",
            "ReferenceEquals(ReadTemplateId, FastAccessReloadRuntime.ReadTemplateId)"
        };
        foreach (string token in required)
            if (!source.Contains(token, StringComparison.Ordinal))
                throw new InvalidOperationException("Reload reachability publication fence regression failed: missing structural lifecycle contract token: " + token);

        if (source.Contains("result = false;", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload reachability publication fence regression failed: publication fence must never manufacture a false result instead of restoring captured vanilla state.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "src", "ReloadReachabilityPublicationFence.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "src", "ReloadReachabilityPublicationFence.cs"))) return nested;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "src", "ReloadReachabilityPublicationFence.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "src", "ReloadReachabilityPublicationFence.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }
}
