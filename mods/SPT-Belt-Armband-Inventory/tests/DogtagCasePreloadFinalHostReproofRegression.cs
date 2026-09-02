using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCasePreloadFinalHostReproofRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag preload final-host reproof regression failed: module root could not be resolved.");

        string item = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));
        int commit = item.IndexOf("private void CommitDogtagSlotExposure", StringComparison.Ordinal);
        int added = commit < 0 ? -1 : item.IndexOf("bool addedHere = filter.Add(DogtagCaseTpl);", commit, StringComparison.Ordinal);
        int firstCommitted = added < 0 ? -1 : item.IndexOf("DogtagCaseHostContract.RequireCommitted(filter);", added, StringComparison.Ordinal);
        int liveIdentity = firstCommitted < 0 ? -1 : item.IndexOf("RequireLiveDogtagHostIdentity(boundary);", firstCommitted + 1, StringComparison.Ordinal);
        int finalCommitted = liveIdentity < 0 ? -1 : item.IndexOf("DogtagCaseHostContract.RequireCommitted(filter);", liveIdentity + 1, StringComparison.Ordinal);
        int cancellation = finalCommitted < 0 ? -1 : item.IndexOf("cancellationToken.ThrowIfCancellationRequested();", finalCommitted + 1, StringComparison.Ordinal);
        int rollback = cancellation < 0 ? -1 : item.IndexOf("if (addedHere)", cancellation + 1, StringComparison.Ordinal);

        if (commit < 0 || added < 0 || firstCommitted < 0 || liveIdentity < 0 || finalCommitted < 0 || cancellation < 0 || rollback < 0
            || !(commit < added && added < firstCommitted && firstCommitted < liveIdentity
                && liveIdentity < finalCommitted && finalCommitted < cancellation && cancellation < rollback))
            throw new InvalidOperationException(
                "Dogtag preload final-host reproof regression failed: owned append must remain inside committed-content -> live-host identity -> committed-content -> cancellation -> rollback ordering.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseItem.cs"))) return nested;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseItem.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }
}
