using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ExactWindowSizingRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        float rcWidth = AccessoryGridPolicy.ExactWindowWidth(1, 63f);
        float rcHeight = AccessoryGridPolicy.ExactWindowHeight(2, 126f);
        if (Math.Abs(rcWidth - 73f) > 0.01f)
            throw new InvalidOperationException("Exact-fit 1-column GridWindow must use the calibrated tight native horizontal chrome.");
        if (Math.Abs(rcHeight - 158f) > 0.01f)
            throw new InvalidOperationException("Exact-fit 2-row GridWindow must match the observed native 158px height.");

        float oneCellWidth = AccessoryGridPolicy.ExactWindowWidth(1, 63f);
        float oneCellHeight = AccessoryGridPolicy.ExactWindowHeight(1, 63f);
        if (Math.Abs(oneCellWidth - 73f) > 0.01f || Math.Abs(oneCellHeight - 95f) > 0.01f)
            throw new InvalidOperationException("1x1 wearable windows must fit one cell plus calibrated native chrome only.");

        float twoCellWidth = AccessoryGridPolicy.ExactWindowWidth(2, 126f);
        if (Math.Abs(twoCellWidth - 136f) > 0.01f)
            throw new InvalidOperationException("2-column wearable windows must scale by cell pitch without reintroducing minimum padding.");

        if (AccessoryGridPolicy.ExactWindowWidth(0) != 0f || AccessoryGridPolicy.ExactWindowHeight(0) != 0f)
            throw new InvalidOperationException("Invalid grid geometry must fail closed instead of creating a window size.");
    }
}
