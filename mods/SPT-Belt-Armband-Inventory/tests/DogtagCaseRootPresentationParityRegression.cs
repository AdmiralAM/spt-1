using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseRootPresentationParityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag Case root presentation parity regression failed: module root could not be resolved.");

        string item = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));

        Require(item, "BackgroundColor = sourceProperties.BackgroundColor,",
            "clone must explicitly retain canonical Dogtag Case root background presentation");
        Require(item, "!Equals(candidateProperties.BackgroundColor, sourceProperties.BackgroundColor)",
            "live publication revalidation must reject post-preload root presentation drift");

        int publicationVerifier = item.IndexOf("public static void RequireCanonicalRegisteredTemplate(TemplateTable templates)", StringComparison.Ordinal);
        int validateCall = publicationVerifier < 0
            ? -1
            : item.IndexOf("ValidateExisting(candidate, source);", publicationVerifier, StringComparison.Ordinal);
        int presentationCheck = item.IndexOf("!Equals(candidateProperties.BackgroundColor, sourceProperties.BackgroundColor)", StringComparison.Ordinal);
        if (publicationVerifier < 0 || validateCall < 0 || presentationCheck < 0)
            throw new InvalidOperationException("Dogtag Case root presentation parity regression failed: canonical publication proof is incomplete.");

        if (item.Contains("candidateProperties.BackgroundColor = sourceProperties.BackgroundColor", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag Case root presentation parity regression failed: publication validation must fail closed, not repair a mutated live template.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string item = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(item)) return current.FullName;
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

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag Case root presentation parity regression failed: " + message + ".");
    }
}
