using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseAbandonFinalReproofRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag abandon final-reproof regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseHostContract.cs"));
        int method = source.IndexOf("public static bool TryAbandonRollbackAuthority", StringComparison.Ordinal);
        int next = source.IndexOf("public static bool TryRollbackOwnedCaseAddition", method, StringComparison.Ordinal);
        if (method < 0 || next <= method)
            throw new InvalidOperationException("Dogtag abandon final-reproof regression failed: abandon boundary is missing.");

        string body = source.Substring(method, next - method).Replace("\r\n", "\n", StringComparison.Ordinal);
        int firstProof = body.IndexOf("HashSet<MongoId> current = SnapshotCurrentFilter(currentFilter);", StringComparison.Ordinal);
        int secondProof = body.IndexOf("HashSet<MongoId> liveBeforeConsume = SnapshotCurrentFilter(currentFilter);", StringComparison.Ordinal);
        int stabilityProof = body.IndexOf("!current.SetEquals(liveBeforeConsume)", StringComparison.Ordinal);
        int expectedProof = body.IndexOf("!liveBeforeConsume.SetEquals(expectedCommitted)", StringComparison.Ordinal);
        int consume = body.IndexOf("RollbackAuthorities.Remove(preCommitSnapshot);", StringComparison.Ordinal);

        if (firstProof < 0 || secondProof <= firstProof || stabilityProof <= secondProof
            || expectedProof <= secondProof || consume <= expectedProof)
            throw new InvalidOperationException("Dogtag abandon final-reproof regression failed: exact live host must be re-proved against both first snapshot and expected committed shape immediately before metadata consume.");

        if (body.IndexOf("RollbackAuthorities.Remove(preCommitSnapshot);", consume + 1, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Dogtag abandon final-reproof regression failed: metadata authority must have one bounded consume point.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "server", "DogtagCaseHostContract.cs")))
                return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseHostContract.cs")))
                return nested;
            current = current.Parent;
        }
        return null;
    }
}
