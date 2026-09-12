using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class R13FastAccessSyncRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        Assert(FastAccessBeltSyncPolicy.ShouldQueue(true, true, true, true), "successful loaded ArmBand equip/remove queues refresh");
        Assert(!FastAccessBeltSyncPolicy.ShouldQueue(false, true, true, true), "failed inventory event cannot refresh fast access");
        Assert(!FastAccessBeltSyncPolicy.ShouldQueue(true, false, true, true), "foreign-owner inventory event cannot refresh fast access");
        Assert(!FastAccessBeltSyncPolicy.ShouldQueue(true, true, false, true), "non-ArmBand compound event remains vanilla");
        Assert(!FastAccessBeltSyncPolicy.ShouldQueue(true, true, true, false), "plain armband event does not refresh grenade fast access");

        MethodInfo add = FastAccessBeltSyncPatches.FindHandler(typeof(ExplicitFastAccessProbe), "IAddHandler", "OnItemAdded");
        MethodInfo remove = FastAccessBeltSyncPatches.FindHandler(typeof(ExplicitFastAccessProbe), "IRemoveHandler", "OnItemRemoved");
        Assert(add != null && add.Name.EndsWith("OnItemAdded", StringComparison.Ordinal), "explicit add handler resolves through its interface map");
        Assert(remove != null && remove.Name.EndsWith("OnItemRemoved", StringComparison.Ordinal), "explicit remove handler resolves through its interface map");

        FieldInfo controller = FastAccessBeltSyncPatches.FindField(typeof(FastAccessProbe), typeof(FakeController), "InventoryController");
        FieldInfo context = FastAccessBeltSyncPatches.FindField(typeof(FastAccessProbe), typeof(FakeContext), "ItemUiContext");
        Assert(controller != null && controller.FieldType == typeof(FakeController), "controller storage resolves by Show parameter type");
        Assert(context != null && context.FieldType == typeof(FakeContext), "context storage resolves by Show parameter type");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("R13 regression failed: " + message);
    }

    interface IAddHandler { void OnItemAdded(object eventArgs); }
    interface IRemoveHandler { void OnItemRemoved(object eventArgs); }

    sealed class ExplicitFastAccessProbe : IAddHandler, IRemoveHandler
    {
        void IAddHandler.OnItemAdded(object eventArgs) { }
        void IRemoveHandler.OnItemRemoved(object eventArgs) { }
    }

    sealed class FakeController { }
    sealed class FakeContext { }

    class FastAccessProbeBase
    {
#pragma warning disable CS0414
        readonly FakeController gclass_0 = new FakeController();
#pragma warning restore CS0414
    }

    sealed class FastAccessProbe : FastAccessProbeBase
    {
#pragma warning disable CS0414
        readonly FakeContext gclass_1 = new FakeContext();
#pragma warning restore CS0414
    }
}
