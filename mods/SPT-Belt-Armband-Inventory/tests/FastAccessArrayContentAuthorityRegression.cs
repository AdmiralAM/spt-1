using System;
using System.Runtime.CompilerServices;

internal static class FastAccessArrayContentAuthorityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var installed = new[] { 1, 2, 15 };
        Array snapshot = SPTBeltArmbandInventory.FastAccessSlotPolicy.CaptureArrayContentSnapshot(installed)
            ?? throw new InvalidOperationException("FastAccess array-content regression failed: snapshot capture returned null for an array.");

        if (ReferenceEquals(installed, snapshot))
            throw new InvalidOperationException("FastAccess array-content regression failed: snapshot retained the caller-owned array reference.");
        if (!SPTBeltArmbandInventory.FastAccessSlotPolicy.HasExactArrayContent(installed, snapshot))
            throw new InvalidOperationException("FastAccess array-content regression failed: unchanged installed content was not accepted.");

        installed[1] = 99;
        if (SPTBeltArmbandInventory.FastAccessSlotPolicy.HasExactArrayContent(installed, snapshot))
            throw new InvalidOperationException("FastAccess array-content regression failed: same-reference in-place mutation inherited snapshot authority.");

        installed[1] = 2;
        if (!SPTBeltArmbandInventory.FastAccessSlotPolicy.HasExactArrayContent(installed, snapshot))
            throw new InvalidOperationException("FastAccess array-content regression failed: helper did not recognize value restoration; lifecycle monotonic rejection is enforced by the caller flag.");

        var wrongLength = new[] { 1, 2 };
        if (SPTBeltArmbandInventory.FastAccessSlotPolicy.HasExactArrayContent(wrongLength, snapshot))
            throw new InvalidOperationException("FastAccess array-content regression failed: wrong cardinality inherited authority.");

        var wrongType = new short[] { 1, 2, 15 };
        if (SPTBeltArmbandInventory.FastAccessSlotPolicy.HasExactArrayContent(wrongType, snapshot))
            throw new InvalidOperationException("FastAccess array-content regression failed: value-equal different runtime array type inherited authority.");

        if (SPTBeltArmbandInventory.FastAccessSlotPolicy.CaptureArrayContentSnapshot(new object()) != null)
            throw new InvalidOperationException("FastAccess array-content regression failed: non-array input produced a snapshot.");
    }
}
