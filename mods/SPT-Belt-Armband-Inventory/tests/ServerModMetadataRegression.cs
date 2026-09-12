using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory.Server;

internal static class ServerModMetadataRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var metadata = new ModMetadata();
        Assert(metadata.ModGuid == "com.admiralam.spt.belt-armband-inventory.server", "server ModGuid drifted");
        Assert(metadata.Name == "B&A&HB #2 MOD SPT Server", "server mod name drifted");
        Assert(metadata.Version.ToString() == "0.2.0", "SPT server mod metadata version must match v0.2.0 candidate");
        Assert(metadata.SptVersion.ToString() == "~4.1.0", "SPT server compatibility range drifted from the 4.1.x line");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Server mod metadata regression failed: " + message);
    }
}
