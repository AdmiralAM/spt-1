using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ServerPackageTargetRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string root = AppContext.BaseDirectory;
        string project = Path.GetFullPath(Path.Combine(root, "..", "..", "..", "..", "server", "SPT-Belt-Armband-Inventory.Server.csproj"));
        if (!File.Exists(project)) return;

        string text = File.ReadAllText(project);
        Assert(text.Contains("SPTushonka.Common\" Version=\"4.1.3\""), "Common package targets 4.1.3");
        Assert(text.Contains("SPTushonka.DI\" Version=\"4.1.3\""), "DI package targets 4.1.3");
        Assert(text.Contains("SPTushonka.Reflection\" Version=\"4.1.3\""), "Reflection package targets 4.1.3");
        Assert(text.Contains("SPTushonka.Server.Core\" Version=\"4.1.3\""), "Server.Core package targets 4.1.3");
        Assert(!text.Contains("SPTarkov."), "legacy package IDs are removed");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Server package target regression failed: " + message);
    }
}
