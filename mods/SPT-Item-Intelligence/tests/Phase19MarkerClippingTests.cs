using System;
using System.IO;

static class Phase19MarkerClippingTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "ItemHoverOverlaySink.cs"));
        Expect(source.Contains("CheckmarkSprite()") && !source.Contains("UnityEngine.UI.Text"), "marker uses shared sprite rather than clipped font glyph", ref assertions);
        Expect(source.Contains("rect.sizeDelta = new Vector2(size, size)"), "sprite preserves square sizing on multi-cell items", ref assertions);
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
        if (!condition) throw new InvalidOperationException("Phase 19 assertion failed: " + message);
    }
}
