using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadBootstrapClosureRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        RequireTarget(typeof(ReloadCallerPublicationFence), "caller publication");
        RequireTarget(typeof(ReloadEpochPublicationFence), "epoch publication");

        if (ReloadBootstrapClosure.ShouldRetryTargetForRegression(true, false)
            || ReloadBootstrapClosure.ShouldRetryTargetForRegression(true, true)
            || ReloadBootstrapClosure.ShouldRetryTargetForRegression(false, true)
            || !ReloadBootstrapClosure.ShouldRetryTargetForRegression(false, false))
            throw new InvalidOperationException("Reload bootstrap closure regression failed: retry truth-table drifted.");
    }

    static void RequireTarget(Type target, string label)
    {
        MethodInfo install = ReloadBootstrapClosure.ResolveTryInstallForRegression(target);
        FieldInfo terminal = ReloadBootstrapClosure.ResolveTerminalFailureForRegression(target);
        if (install == null || install.ReturnType != typeof(bool) || install.GetParameters().Length != 0)
            throw new InvalidOperationException("Reload bootstrap closure regression failed: exact " + label + " TryInstall contract was not resolved.");
        if (terminal == null || terminal.FieldType != typeof(bool))
            throw new InvalidOperationException("Reload bootstrap closure regression failed: exact " + label + " terminalFailure contract was not resolved.");
    }
}
