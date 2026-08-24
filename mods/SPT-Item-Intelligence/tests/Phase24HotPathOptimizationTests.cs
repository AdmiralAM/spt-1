using System;
using System.IO;

static class Phase24HotPathOptimizationTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string renderer = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "PolishedTooltipRenderer.cs"));

        Expect(renderer.Contains("static string[] lineBuffer") && renderer.Contains("static float[] rowHeightBuffer"),
            "tooltip renderer reuses line and row-height buffers across repaint calls", ref assertions);
        Expect(renderer.Contains("EnsureBuffers(lineCount)"),
            "tooltip buffers grow only when line capacity is insufficient", ref assertions);
        Expect(!renderer.Contains("string[] lines = new string[lineCount]") && !renderer.Contains("float[] rowHeights = new float[lineCount]"),
            "tooltip repaint path does not allocate per-draw line arrays", ref assertions);
        Expect(renderer.Contains("static GUIStyle cachedLabel") && renderer.Contains("static GUIStyle cachedSemanticLabel"),
            "tooltip renderer caches GUI styles instead of rebuilding them every repaint", ref assertions);
        Expect(renderer.Contains("object.ReferenceEquals(cachedSkin, skin)"),
            "cached styles are rebuilt only when the active GUI skin changes", ref assertions);
        Expect(renderer.Contains("clipping = TextClipping.Clip") && renderer.Contains("wordWrap = true") && renderer.Contains("label.CalcHeight"),
            "performance pass preserves the established tooltip geometry contract", ref assertions);
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
        if (!condition) throw new InvalidOperationException("Phase 24 assertion failed: " + message);
    }
}
