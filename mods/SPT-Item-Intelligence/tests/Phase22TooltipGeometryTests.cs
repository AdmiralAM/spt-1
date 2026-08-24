using System;
using System.IO;

static class Phase22TooltipGeometryTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string renderer = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "PolishedTooltipRenderer.cs"));

        Expect(renderer.Contains("clipping = TextClipping.Clip"),
            "tooltip text is constrained to the panel instead of bleeding outside it", ref assertions);
        Expect(renderer.Contains("wordWrap = true"),
            "long tooltip lines wrap inside the panel", ref assertions);
        Expect(renderer.Contains("label.CalcHeight"),
            "wrapped rows participate in dynamic height measurement", ref assertions);
        Expect(renderer.Contains("ScreenMargin") && renderer.Contains("Screen.width - ScreenMargin") && renderer.Contains("Screen.height - ScreenMargin"),
            "tooltip panel keeps a screen-edge safety margin", ref assertions);
        Expect(renderer.Contains("padding = new RectOffset(0, 0, 1, 2)"),
            "label geometry reserves vertical glyph room for ascenders and descenders", ref assertions);
        Expect(renderer.Contains("float yCursor") && renderer.Contains("yCursor += rowHeights[i] + rowGap"),
            "rows advance by measured height instead of a fixed line grid", ref assertions);
        Expect(renderer.Contains("minimumWidth = 200f * scale") && renderer.Contains("preferredMaximumWidth = 430f * scale"),
            "tooltip remains compact while retaining a bounded readable width", ref assertions);
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
        if (!condition) throw new InvalidOperationException("Phase 22 assertion failed: " + message);
    }
}
