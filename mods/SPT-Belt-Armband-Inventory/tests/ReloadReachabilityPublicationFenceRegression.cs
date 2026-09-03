using System;
using System.Collections;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadReachabilityPublicationFenceRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
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

            // Exact reference restoration alone may make an identity snapshot current again;
            // lifecycle generation is the ABA authority that prevents Reset/reinstall revival.
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
        }
        finally
        {
            ReloadReachabilityPublicationFence.ResetForRegression();
            FastAccessReloadRuntime.Reset();
        }
    }
}
