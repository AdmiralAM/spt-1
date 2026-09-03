using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseRollbackFinalReproofRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag rollback final-reproof regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseHostContract.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        int method = source.IndexOf("public static bool TryRollbackOwnedCaseAddition", StringComparison.Ordinal);
        int next = source.IndexOf("private static HashSet<MongoId> SnapshotCurrentFilter", method, StringComparison.Ordinal);
        if (method < 0 || next <= method)
            throw new InvalidOperationException("Dogtag rollback final-reproof regression failed: rollback method boundary missing.");

        string body = source.Substring(method, next - method);
        int expected = body.IndexOf("HashSet<MongoId> expectedCommitted", StringComparison.Ordinal);
        int firstSnapshot = body.IndexOf("HashSet<MongoId> current = SnapshotCurrentFilter(currentFilter);", expected, StringComparison.Ordinal);
        int firstProof = body.IndexOf("current.SetEquals(expectedCommitted)", firstSnapshot, StringComparison.Ordinal);
        int finalSnapshot = body.IndexOf("HashSet<MongoId> liveBeforeRemove = SnapshotCurrentFilter(currentFilter);", firstProof, StringComparison.Ordinal);
        int finalStability = body.IndexOf("current.SetEquals(liveBeforeRemove)", finalSnapshot, StringComparison.Ordinal);
        int finalExpected = body.IndexOf("liveBeforeRemove.SetEquals(expectedCommitted)", finalStability, StringComparison.Ordinal);
        int mutation = body.IndexOf("currentFilter.Remove(caseTpl)", finalExpected, StringComparison.Ordinal);
        int after = body.IndexOf("HashSet<MongoId> after = SnapshotCurrentFilter(currentFilter);", mutation, StringComparison.Ordinal);

        if (!(expected >= 0 && firstSnapshot > expected && firstProof > firstSnapshot
            && finalSnapshot > firstProof && finalStability > finalSnapshot
            && finalExpected > finalStability && mutation > finalExpected && after > mutation))
            throw new InvalidOperationException("Dogtag rollback final-reproof regression failed: committed-shape proof -> final point-in-time reproof -> owned remove -> post-state proof ordering changed.");

        if (Count(body, "SnapshotCurrentFilter(currentFilter)") < 3)
            throw new InvalidOperationException("Dogtag rollback final-reproof regression failed: rollback must independently snapshot initial committed shape, immediate pre-remove shape and post-remove shape.");
    }

    private static int Count(string source, string token)
    {
        int count = 0;
        int offset = 0;
        while (true)
        {
            int index = source.IndexOf(token, offset, StringComparison.Ordinal);
            if (index < 0) return count;
            count++;
            offset = index + token.Length;
        }
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "server", "DogtagCaseHostContract.cs"))) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseHostContract.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseHostContract.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }
}
