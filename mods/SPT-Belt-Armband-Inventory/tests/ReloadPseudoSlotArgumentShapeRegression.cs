using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadPseudoSlotArgumentShapeRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        MethodInfo create = typeof(FastAccessSlotPatches).GetMethod(
            "CreateSingleSlotArgument",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload pseudo-slot argument regression failed: CreateSingleSlotArgument boundary missing.");

        object pseudoSlot = Enum.ToObject(typeof(FakeEquipmentSlot), RuntimeIdentity.DedicatedBeltEquipmentSlotValue);
        Assert(Convert.ToInt32(pseudoSlot) == RuntimeIdentity.DedicatedBeltEquipmentSlotValue,
            "test pseudo-slot must preserve the exact dedicated Belt wire value");
        Assert(!Enum.IsDefined(typeof(FakeEquipmentSlot), pseudoSlot),
            "test pseudo-slot must remain intentionally undeclared to exercise the SPT slot15 boundary");

        object? arrayArgument = create.Invoke(null, new object[]
        {
            typeof(FakeEquipmentSlot[]),
            typeof(FakeEquipmentSlot),
            pseudoSlot
        });
        Assert(arrayArgument is FakeEquipmentSlot[],
            "array boundary must produce the exact slot-enum array type");
        var array = (FakeEquipmentSlot[])arrayArgument!;
        Assert(array.Length == 1 && Convert.ToInt32(array[0]) == RuntimeIdentity.DedicatedBeltEquipmentSlotValue,
            "array boundary must carry exactly one pseudo-slot15 value without declared-enum gating");

        object? enumerableArgument = create.Invoke(null, new object[]
        {
            typeof(IEnumerable<FakeEquipmentSlot>),
            typeof(FakeEquipmentSlot),
            pseudoSlot
        });
        Assert(enumerableArgument is List<FakeEquipmentSlot>,
            "IEnumerable boundary must use a concrete List<T> accepted by the declared parameter contract");
        var list = ((IEnumerable<FakeEquipmentSlot>)enumerableArgument!).ToArray();
        Assert(list.Length == 1 && Convert.ToInt32(list[0]) == RuntimeIdentity.DedicatedBeltEquipmentSlotValue,
            "IEnumerable boundary must carry exactly one pseudo-slot15 value");

        object? incompatible = create.Invoke(null, new object[]
        {
            typeof(HashSet<FakeEquipmentSlot>),
            typeof(FakeEquipmentSlot),
            pseudoSlot
        });
        Assert(incompatible == null,
            "unsupported concrete collection shape must fail closed instead of inventing a broader slot query");
    }

    private enum FakeEquipmentSlot
    {
        FirstPrimaryWeapon = 0,
        ArmBand = 14
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Reload pseudo-slot argument regression failed: " + message + ".");
    }
}
