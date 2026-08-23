using System;
using System.IO;

static class Phase19MarkerClippingTests
{
    public static int Run()
    {
        int assertions = 0;
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "mods", "SPT-Item-Intelligence", "src", "ItemHoverOverlaySink.cs"));
        Expect(source.Contains("horizontalOverflow\", Enum.Parse(PropertyType(text, \"horizontalOverflow\"), \"Overflow\")"), "marker text enables horizontal overflow", ref assertions);
        Expect(source.Contains("verticalOverflow\", Enum.Parse(PropertyType(text, \"verticalOverflow\"), \"Overflow\")"), "marker text enables vertical overflow", ref assertions);
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
