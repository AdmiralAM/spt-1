using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseOptionalPublicationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string root = FindModuleRoot()
            ?? throw new InvalidOperationException("Dogtag optional publication regression failed: module root could not be resolved.");

        string preflight = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseCanonicalFilterPreflight.cs"));
        string item = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));
        string assort = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAssort.cs"));
        string guards = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseHostExclusionGuard.cs"));
        string availability = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAvailability.cs"));

        Require(preflight, "catch (InvalidOperationException exception)", "a foreign canonical-contract mismatch must disable only the optional component");
        Require(preflight, "DogtagCaseAvailability.MarkUnavailable(exception.Message)", "preflight must publish the unavailable state before later callbacks");
        Require(preflight, "no foreign item or filter was changed", "the diagnostic must state the isolation boundary");
        Require(item, "if (!DogtagCaseAvailability.IsAvailable)", "item registration must honor the preflight gate");
        Require(item, "Dogtag Case registration skipped", "item registration must emit a bounded diagnostic");
        Require(assort, "if (!DogtagCaseAvailability.IsAvailable)", "offer registration must honor the same gate");
        Require(assort, "Dogtag Case offer skipped", "offer registration must emit a bounded diagnostic");
        Require(guards, "if (!DogtagCaseAvailability.IsAvailable)", "both post-registration host guards must honor the same gate");
        Require(availability, "Interlocked.CompareExchange(ref unavailableReason, reason, null)", "the first failure reason must be stable for the process");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseAvailability.cs"))) return nested;
            if (File.Exists(Path.Combine(current.FullName, "server", "DogtagCaseAvailability.cs"))) return current.FullName;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag optional publication regression failed: " + message + ".");
    }
}
