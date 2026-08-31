using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadDiscoveryDiagnosticIsolationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        bool infoCalled = false;
        bool warningCalled = false;

        ReloadDiagnosticLog.TryInfo(_ =>
        {
            infoCalled = true;
            throw new InvalidOperationException("synthetic info sink failure");
        }, "info");
        ReloadDiagnosticLog.TryWarning(_ =>
        {
            warningCalled = true;
            throw new InvalidOperationException("synthetic warning sink failure");
        }, "warning");

        if (!infoCalled || !warningCalled)
            throw new InvalidOperationException("Reload discovery diagnostic regression failed: diagnostic sinks were not invoked before isolation.");

        var patches = new FastAccessSlotPatches(
            _ => throw new InvalidOperationException("synthetic install info sink failure"),
            _ => throw new InvalidOperationException("synthetic install warning sink failure"));

        MethodInfo fail = typeof(FastAccessSlotPatches).GetMethod("Fail", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload discovery diagnostic regression failed: Fail boundary is missing.");

        object result = fail.Invoke(patches, new object[] { "synthetic fail-closed discovery" });
        if (result is not bool value || value)
            throw new InvalidOperationException("Reload discovery diagnostic regression failed: throwing warning sink changed the fail-closed false result.");

        patches.Dispose();
    }
}
