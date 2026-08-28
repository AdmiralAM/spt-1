using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class DedicatedWearableWindowSizingRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        AssertExact("ArmBand", 1, 2, 87f, 160f);
        AssertExact("Belt", 2, 2, 150f, 160f);
        AssertExact("HeadBand", 1, 1, 87f, 97f);

        if (AccessoryGridPolicy.CellCount(2, 2) != 4)
            throw new InvalidOperationException("Dedicated Belt must retain exact 2x2 / four-cell capacity.");
        if (AccessoryGridPolicy.CellCount(1, 1) != 1)
            throw new InvalidOperationException("Dedicated HeadBand must retain exact 1x1 / one-cell capacity.");
    }

    static void AssertExact(string category, int columns, int rows, float expectedWidth, float expectedHeight)
    {
        float width = AccessoryGridPolicy.ExactWindowWidth(columns);
        float height = AccessoryGridPolicy.ExactWindowHeight(rows);
        if (Math.Abs(width - expectedWidth) > 0.01f || Math.Abs(height - expectedHeight) > 0.01f)
            throw new InvalidOperationException(category + " GridWindow must fit its declared cells exactly with native chrome only.");
    }
}
