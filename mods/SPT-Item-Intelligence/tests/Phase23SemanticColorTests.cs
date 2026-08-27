using System;
using System.IO;

static class Phase23SemanticColorTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string renderer = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "PolishedTooltipRenderer.cs"));

        Expect(renderer.Contains("richText = false") && renderer.Contains("richText = true") && renderer.Contains("cachedSemanticLabel"),
            "semantic progress uses a dedicated rich-text style while ordinary labels remain plain", ref assertions);
        Expect(renderer.Contains("GetCachedSemanticLine(line, semantic)") && renderer.Contains("ApplySemanticProgressColor(line, color)"),
            "semantic formatting remains render-time presentation but steady-state repaint reuses its cached rendered string", ref assertions);
        Expect(renderer.Contains("TryReadNextRatio") && renderer.Contains("while (TryReadNextRatio"),
            "semantic state scans every progress ratio instead of trusting only the first", ref assertions);
        Expect(renderer.Contains("if (allComplete) return settings.CompleteColor") &&
               renderer.Contains("if (!anyProgress) return settings.MissingColor") &&
               renderer.Contains("return settings.PartialColor"),
            "complete, missing, and partial states have explicit aggregate semantics", ref assertions);
        Expect(renderer.Contains("<color=#") && renderer.Contains("</color>"),
            "progress values are colored without tinting the whole label", ref assertions);
        Expect(renderer.Contains("colored.EndsWith(\"✓\"") && renderer.Contains(">✓</color>"),
            "completion checkmark receives the semantic color", ref assertions);
        Expect(renderer.Contains("activeStyle.normal.textColor = Color.white"),
            "labels remain white even when progress values are semantic-colored", ref assertions);
        return assertions;
    }

    static string FindRepositoryRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "mods", "SPT-Item-Intelligence"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 23 assertion failed: " + message);
    }
}
