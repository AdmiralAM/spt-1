using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class RuntimeListOwnershipRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var tracker = new RuntimeListOwnership();
        object owner = new object();
        object list = new object();
        object entry = new object();

        Assert(!tracker.Owns(owner, list, entry), "unmarked runtime slot entry is external");
        tracker.Mark(owner, list, entry);
        Assert(tracker.Owns(owner, list, entry), "marked runtime slot entry is Belt-owned");
        Assert(!tracker.Owns(owner, new object(), entry), "replacement list invalidates Belt ownership");
        Assert(!tracker.Owns(owner, list, new object()), "replacement entry invalidates Belt ownership");
        tracker.Forget(owner);
        Assert(!tracker.Owns(owner, list, entry), "forgotten runtime slot entry is no longer Belt-owned");
        tracker.Mark(owner, list, entry);
        tracker.Reset();
        Assert(!tracker.Owns(owner, list, entry), "runtime ownership reset releases all Belt claims");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("RuntimeListOwnership regression failed: " + message);
    }
}