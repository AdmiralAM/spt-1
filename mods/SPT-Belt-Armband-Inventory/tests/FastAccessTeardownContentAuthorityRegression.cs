using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class FastAccessTeardownContentAuthorityRegression
{
    private static object LiveArray;

    [ModuleInitializer]
    internal static void Run()
    {
        int[] original = { 1, 2 };
        int[] installed = { 1, 2, 15 };
        Array snapshot = FastAccessSlotPolicy.CaptureArrayContentSnapshot(installed);
        LiveArray = installed;

        var patches = new FastAccessSlotPatches(null, null);
        MethodInfo restore = typeof(FastAccessSlotPatches).GetMethod(
            "RestoreOwnedWrite",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("fast-access teardown authority regression failed: RestoreOwnedWrite is missing");
        FieldInfo liveField = typeof(FastAccessTeardownContentAuthorityRegression).GetField(
            nameof(LiveArray),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("fast-access teardown authority regression failed: synthetic live field is missing");
        FieldInfo unsafeField = typeof(FastAccessSlotPatches).GetField(
            "arrayContentAuthorityUnsafe",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("fast-access teardown authority regression failed: terminal content-authority fence is missing");

        installed[0] = 99;
        object[] args = { liveField, original, installed, snapshot, true };
        bool proven = (bool)(restore.Invoke(patches, args)
            ?? throw new InvalidOperationException("fast-access teardown authority regression failed: RestoreOwnedWrite returned null"));
        bool wrote = (bool)args[4];
        if (proven || !wrote || !ReferenceEquals(LiveArray, installed))
            throw new InvalidOperationException("fast-access teardown authority regression failed: same-reference content drift was restored or ownership was released");
        if (!(bool)(unsafeField.GetValue(patches) ?? false))
            throw new InvalidOperationException("fast-access teardown authority regression failed: first content drift did not make lifecycle authority terminally unsafe");

        installed[0] = 1;
        if (!FastAccessSlotPolicy.HasExactArrayContent(installed, snapshot))
            throw new InvalidOperationException("fast-access teardown authority regression failed: synthetic ABA restoration did not restore installed values");
        if (!(bool)(unsafeField.GetValue(patches) ?? false))
            throw new InvalidOperationException("fast-access teardown authority regression failed: ABA restoration incorrectly cleared terminal content-authority state");
        if (patches.TryInstall())
            throw new InvalidOperationException("fast-access teardown authority regression failed: ABA-restored lifecycle was allowed to reinstall");
        if (!ReferenceEquals(LiveArray, installed))
            throw new InvalidOperationException("fast-access teardown authority regression failed: terminal reinstall refusal mutated the synthetic live array");
    }
}
