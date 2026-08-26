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
        if (Math.Abs(rcWidth - 87f) > 0.01f)
            throw new InvalidOperationException("Exact-fit 1-column GridWindow must not retain the old 96px artificial minimum width.");
        if (Math.Abs(rcHeight - 160f) > 0.01f)
            throw new InvalidOperationException("Exact-fit 2-row GridWindow must equal measured grid height plus native chrome only.");

        float oneCellWidth = AccessoryGridPolicy.ExactWindowWidth(1, 63f);
        float oneCellHeight = AccessoryGridPolicy.ExactWindowHeight(1, 63f);
        if (Math.Abs(oneCellWidth - 87f) > 0.01f || Math.Abs(oneCellHeight - 97f) > 0.01f)
            throw new InvalidOperationException("Future 1x1 wearable windows must size directly from their cells without minimum clamps.");

        if (AccessoryGridPolicy.ExactWindowWidth(0) != 0f || AccessoryGridPolicy.ExactWindowHeight(0) != 0f)
            throw new InvalidOperationException("Invalid grid geometry must fail closed instead of creating a window size.");
    }
}
