using System;
using System.IO;

static class Phase25GuiContentReuseTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string renderer = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "PolishedTooltipRenderer.cs"));

        Expect(renderer.Contains("static readonly GUIContent measureContent = new GUIContent()"),
            "tooltip measurement reuses one GUIContent instance", ref assertions);
        Expect(renderer.Contains("measureContent.text = line") && renderer.Contains("label.CalcSize(measureContent)"),
            "width measurement avoids allocating GUIContent per line", ref assertions);
        Expect(renderer.Contains("measureContent.text = lineBuffer[i]") && renderer.Contains("label.CalcHeight(measureContent, textWidth)"),
            "wrapped-height measurement avoids allocating GUIContent per line", ref assertions);
        Expect(!renderer.Contains("new GUIContent(line)") && !renderer.Contains("new GUIContent(lineBuffer[i])"),
            "repaint path contains no per-line GUIContent construction", ref assertions);
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
        if (!condition) throw new InvalidOperationException("Phase 25 assertion failed: " + message);
    }
}
