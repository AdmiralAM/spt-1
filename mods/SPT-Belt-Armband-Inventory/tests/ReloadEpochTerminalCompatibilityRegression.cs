using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ReloadEpochTerminalCompatibilityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Reload epoch terminal compatibility regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "src", "ReloadScopeEpochGuard.cs"));

        Require(source, "if (harmonyType == null || harmonyMethodType == null) return false;",
            "late Harmony assembly availability must remain retryable");
        Require(source, "if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || unpatchSelf == null)\n                        terminalFailure = true;",
            "loaded-but-structurally-incompatible Harmony contract must become terminal fail-closed");
        Require(source, "if (enter == null || exit == null || append == null || reset == null)",
            "runtime callback shape must remain a bounded structural gate");
        Require(source, "if (terminalFailure) AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;",
            "terminal incompatibility must detach the AssemblyLoad retry handler");
        Require(source, "terminalFailure = owner != null && !rolledBack;",
            "partial Harmony mutation must remain retryable only when owner rollback was proven");

        int harmonyPresent = source.IndexOf("if (harmonyType == null || harmonyMethodType == null) return false;", StringComparison.Ordinal);
        int structuralGate = source.IndexOf("if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || unpatchSelf == null)", StringComparison.Ordinal);
        int terminalAssignment = structuralGate < 0 ? -1 : source.IndexOf("terminalFailure = true;", structuralGate, StringComparison.Ordinal);
        int structuralReturn = terminalAssignment < 0 ? -1 : source.IndexOf("if (harmonyCtor == null || harmonyMethodCtor == null || patch == null || unpatchSelf == null) return false;", terminalAssignment, StringComparison.Ordinal);
        int ownerCreate = source.IndexOf("owner = harmonyCtor.Invoke", StringComparison.Ordinal);
        if (harmonyPresent < 0 || structuralGate < 0 || terminalAssignment < 0 || structuralReturn < 0 || ownerCreate < 0
            || !(harmonyPresent < structuralGate && structuralGate < terminalAssignment && terminalAssignment < structuralReturn && structuralReturn < ownerCreate))
            throw new InvalidOperationException("Reload epoch terminal compatibility regression failed: structural incompatibility must become terminal and return before any Harmony owner/process-wide mutation is created.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string source = Path.Combine(current.FullName, "src", "ReloadScopeEpochGuard.cs");
            if (File.Exists(source)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "src", "ReloadScopeEpochGuard.cs");
            if (File.Exists(direct)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "src", "ReloadScopeEpochGuard.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Reload epoch terminal compatibility regression failed: " + message + ".");
    }
}
