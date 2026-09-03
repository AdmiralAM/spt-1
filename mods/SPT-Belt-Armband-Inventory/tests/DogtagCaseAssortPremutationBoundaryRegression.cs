using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseAssortPremutationBoundaryRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string source = ReadSource("server", "DogtagCaseAssort.cs").Replace("\r\n", "\n", StringComparison.Ordinal);

        int tryBlock = source.IndexOf("        try\n        {", StringComparison.Ordinal);
        int firstMutation = source.IndexOf("            items.Add(offer);", tryBlock, StringComparison.Ordinal);
        if (tryBlock < 0 || firstMutation < 0)
            throw new InvalidOperationException("Dogtag assort premutation regression failed: new-offer transaction shape is missing.");

        string prefix = source[tryBlock..firstMutation];
        int cancellationBefore = prefix.IndexOf("cancellationToken.ThrowIfCancellationRequested();", StringComparison.Ordinal);
        int publicationBoundary = prefix.IndexOf("RequirePublicationBoundary(templateTable, templateId);", cancellationBefore + 1, StringComparison.Ordinal);
        int wrapperProof = prefix.IndexOf("RequireAssortWrapperIdentity();", publicationBoundary + 1, StringComparison.Ordinal);
        int cancellationAfter = prefix.IndexOf("cancellationToken.ThrowIfCancellationRequested();", wrapperProof + 1, StringComparison.Ordinal);

        if (min(cancellationBefore, publicationBoundary, wrapperProof, cancellationAfter) < 0
            || !(cancellationBefore < publicationBoundary
                && publicationBoundary < wrapperProof
                && wrapperProof < cancellationAfter))
            throw new InvalidOperationException("Dogtag assort premutation regression failed: final cancellation -> canonical/host -> Ragman-wrapper -> cancellation proof does not precede first mutation.");

        if (Count(prefix, "RequirePublicationBoundary(templateTable, templateId);") != 1)
            throw new InvalidOperationException("Dogtag assort premutation regression failed: bounded pre-mutation region must contain exactly one final canonical/host reproof.");

        int initialBoundary = source.IndexOf("RequirePublicationBoundary(templateTable, templateId);", StringComparison.Ordinal);
        if (initialBoundary < 0 || initialBoundary >= tryBlock)
            throw new InvalidOperationException("Dogtag assort premutation regression failed: initial fail-closed publication preflight disappeared.");

        int postMutationBoundary = source.IndexOf("RequirePublicationBoundary(templateTable, templateId);", firstMutation + 1, StringComparison.Ordinal);
        if (postMutationBoundary < 0)
            throw new InvalidOperationException("Dogtag assort premutation regression failed: post-publication canonical/host verification disappeared.");
    }

    private static int min(params int[] values)
    {
        int result = int.MaxValue;
        foreach (int value in values)
        {
            if (value < result) result = value;
        }
        return result;
    }

    private static int Count(string text, string token)
    {
        int count = 0;
        int offset = 0;
        while (true)
        {
            int found = text.IndexOf(token, offset, StringComparison.Ordinal);
            if (found < 0) return count;
            count++;
            offset = found + token.Length;
        }
    }

    private static string ReadSource(params string[] parts)
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory", Path.Combine(parts));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory", Path.Combine(parts));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            current = current.Parent;
        }

        throw new InvalidOperationException("Dogtag assort premutation regression failed: source file could not be resolved.");
    }
}
