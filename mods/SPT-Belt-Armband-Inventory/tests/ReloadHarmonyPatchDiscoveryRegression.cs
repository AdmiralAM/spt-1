using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadHarmonyPatchDiscoveryRegression
{
    sealed class FakeHarmonyMethod { }

    sealed class EpochUniquePatchHost
    {
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null) { }
        public void Patch(string original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null) { }
    }

    sealed class EpochMissingPostfixHost
    {
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null) { }
    }

    sealed class EpochAmbiguousPatchHost
    {
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null) { }
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null, int priority = 0) { }
    }

    sealed class ReloadUniquePatchHost
    {
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null, FakeHarmonyMethod transpiler = null, FakeHarmonyMethod finalizer = null) { }
        public void Patch(string original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null, FakeHarmonyMethod finalizer = null) { }
        public void UnpatchSelf() { }
    }

    sealed class ReloadMissingFinalizerHost
    {
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null) { }
        public void UnpatchSelf() { }
    }

    sealed class ReloadAmbiguousPatchHost
    {
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null, FakeHarmonyMethod finalizer = null) { }
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null, FakeHarmonyMethod finalizer = null, int priority = 0) { }
        public void UnpatchSelf() { }
    }

    class ZeroArgBase
    {
        public void UnpatchSelf() { }
    }

    sealed class ZeroArgAmbiguousHost : ZeroArgBase
    {
        public new void UnpatchSelf() { }
    }

    [ModuleInitializer]
    internal static void Run()
    {
        ExerciseEpochFinder();
        ExercisePrimaryReloadFinder();
    }

    static void ExerciseEpochFinder()
    {
        MethodInfo finder = typeof(ReloadScopeEpochGuard).GetMethod("FindPatchMethod", BindingFlags.Static | BindingFlags.NonPublic);
        if (finder == null)
            throw new InvalidOperationException("Reload epoch Harmony patch finder regression surface is missing.");

        MethodInfo unique = (MethodInfo)finder.Invoke(null, new object[] { typeof(EpochUniquePatchHost), typeof(FakeHarmonyMethod) });
        if (unique == null)
            throw new InvalidOperationException("Reload epoch guard must select a single compatible Harmony.Patch signature.");
        ParameterInfo[] uniqueParameters = unique.GetParameters();
        if (uniqueParameters.Length != 3 || uniqueParameters[0].ParameterType != typeof(MethodBase))
            throw new InvalidOperationException("Reload epoch Harmony.Patch selection chose the wrong overload.");

        if ((MethodInfo)finder.Invoke(null, new object[] { typeof(EpochMissingPostfixHost), typeof(FakeHarmonyMethod) }) != null)
            throw new InvalidOperationException("Reload epoch Harmony.Patch discovery must fail closed when postfix is absent.");
        if ((MethodInfo)finder.Invoke(null, new object[] { typeof(EpochAmbiguousPatchHost), typeof(FakeHarmonyMethod) }) != null)
            throw new InvalidOperationException("Reload epoch Harmony.Patch discovery must fail closed on compatible overload ambiguity.");
    }

    static void ExercisePrimaryReloadFinder()
    {
        MethodInfo finder = typeof(FastAccessSlotPatches).GetMethod("FindPatchMethod", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo unpatchFinder = typeof(FastAccessSlotPatches).GetMethod("FindZeroArgInstanceMethod", BindingFlags.Static | BindingFlags.NonPublic);
        if (finder == null || unpatchFinder == null)
            throw new InvalidOperationException("Primary reload Harmony discovery regression surface is missing.");

        MethodInfo unique = (MethodInfo)finder.Invoke(null, new object[] { typeof(ReloadUniquePatchHost), typeof(FakeHarmonyMethod) });
        if (unique == null)
            throw new InvalidOperationException("Primary reload owner must select the one Patch signature carrying prefix/postfix/finalizer.");
        ParameterInfo[] parameters = unique.GetParameters();
        if (parameters.Length != 5 || parameters[0].ParameterType != typeof(MethodBase))
            throw new InvalidOperationException("Primary reload owner selected the wrong Harmony.Patch overload.");

        if ((MethodInfo)finder.Invoke(null, new object[] { typeof(ReloadMissingFinalizerHost), typeof(FakeHarmonyMethod) }) != null)
            throw new InvalidOperationException("Primary reload Harmony.Patch discovery must fail closed when finalizer is absent.");
        if ((MethodInfo)finder.Invoke(null, new object[] { typeof(ReloadAmbiguousPatchHost), typeof(FakeHarmonyMethod) }) != null)
            throw new InvalidOperationException("Primary reload Harmony.Patch discovery must fail closed when multiple fully-compatible overloads exist.");

        MethodInfo unpatch = (MethodInfo)unpatchFinder.Invoke(null, new object[] { typeof(ReloadUniquePatchHost), "UnpatchSelf" });
        if (unpatch == null || unpatch.DeclaringType != typeof(ReloadUniquePatchHost))
            throw new InvalidOperationException("Primary reload owner must resolve one exact zero-arg UnpatchSelf boundary.");
        if ((MethodInfo)unpatchFinder.Invoke(null, new object[] { typeof(ZeroArgAmbiguousHost), "UnpatchSelf" }) != null)
            throw new InvalidOperationException("Primary reload UnpatchSelf discovery must fail closed on inherited zero-arg ambiguity.");
    }
}
