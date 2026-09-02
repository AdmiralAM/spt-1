using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseTraderFinalHostReproofRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag trader final-host reproof regression failed: module root could not be resolved.");

        string assort = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAssort.cs"));
        const string committed = "DogtagCaseHostContract.RequireCommitted(hostFilter);";
        const string filterIdentity = "!ReferenceEquals(liveGroups[0].Filter, hostFilter)";

        int firstCommitted = assort.IndexOf(committed, StringComparison.Ordinal);
        int liveFilterIdentity = firstCommitted < 0
            ? -1
            : assort.IndexOf(filterIdentity, firstCommitted + 1, StringComparison.Ordinal);
        int finalCommitted = liveFilterIdentity < 0
            ? -1
            : assort.IndexOf(committed, liveFilterIdentity + 1, StringComparison.Ordinal);

        if (firstCommitted < 0 || liveFilterIdentity < 0 || finalCommitted < 0
            || !(firstCommitted < liveFilterIdentity && liveFilterIdentity < finalCommitted))
            throw new InvalidOperationException(
                "Dogtag trader final-host reproof regression failed: committed content proof must bracket the live filter reference-identity reproof.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs");
            if (File.Exists(direct)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseAssort.cs"))) return nested;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs");
            if (File.Exists(direct)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseAssort.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }
}
