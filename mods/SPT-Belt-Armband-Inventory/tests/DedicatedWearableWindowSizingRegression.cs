using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class DedicatedWearableWindowSizingRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        AssertExact("ArmBand", 1, 2, 73f, 158f);
        AssertExact("Belt", 2, 2, 136f, 158f);
        AssertExact("HeadBand split strip", 2, 1, 136f, 95f);

        if (AccessoryGridPolicy.CellCount(2, 2) != 4)
            throw new InvalidOperationException("Dedicated Belt must retain exact 2x2 / four-cell capacity.");
        if (AccessoryGridPolicy.CellCount(2, 1) != 2)
            throw new InvalidOperationException("Dedicated HeadBand window must present its two separately filtered 1x1 grids as one horizontal strip.");

        string source = System.IO.File.ReadAllText(FindModuleFile("src", "GridWindowSizingPatches.cs"));
        if (!source.Contains("ApplySplitHeadBandContent(window)", StringComparison.Ordinal)
            || !source.Contains("containedRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cell)", StringComparison.Ordinal))
            throw new InvalidOperationException("Dedicated HeadBand must normalize both native grid views and their content frame to one 2x1 strip.");
    }

    static void AssertExact(string category, int columns, int rows, float expectedWidth, float expectedHeight)
    {
        float width = AccessoryGridPolicy.ExactWindowWidth(columns);
        float height = AccessoryGridPolicy.ExactWindowHeight(rows);
        if (Math.Abs(width - expectedWidth) > 0.01f || Math.Abs(height - expectedHeight) > 0.01f)
            throw new InvalidOperationException(category + " GridWindow must fit its declared cells exactly with calibrated native chrome only.");
    }

    static string FindModuleFile(params string[] parts)
    {
        System.IO.DirectoryInfo current = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = current.FullName;
            for (int index = 0; index < parts.Length; index++)
                candidate = System.IO.Path.Combine(candidate, parts[index]);
            if (System.IO.File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        throw new System.IO.DirectoryNotFoundException("Could not locate Belt module root.");
    }
}
