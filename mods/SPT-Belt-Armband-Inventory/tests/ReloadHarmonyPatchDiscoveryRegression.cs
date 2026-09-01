using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadHarmonyPatchDiscoveryRegression
{
    sealed class FakeHarmonyMethod { }

    sealed class UniquePatchHost
    {
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null) { }
        public void Patch(string original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null) { }
    }

    sealed class MissingPostfixHost
    {
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null) { }
    }

    sealed class AmbiguousPatchHost
    {
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null) { }
        public void Patch(MethodBase original, FakeHarmonyMethod prefix = null, FakeHarmonyMethod postfix = null, int priority = 0) { }
    }

    [ModuleInitializer]
    internal static void Run()
    {
        MethodInfo finder = typeof(ReloadScopeEpochGuard).GetMethod("FindPatchMethod", BindingFlags.Static | BindingFlags.NonPublic);
        if (finder == null)
            throw new InvalidOperationException("Reload Harmony patch finder regression surface is missing.");

        MethodInfo unique = (MethodInfo)finder.Invoke(null, new object[] { typeof(UniquePatchHost), typeof(FakeHarmonyMethod) });
        if (unique == null)
            throw new InvalidOperationException("A single compatible Harmony.Patch signature must be selected.");
        ParameterInfo[] uniqueParameters = unique.GetParameters();
        if (uniqueParameters.Length != 3 || uniqueParameters[0].ParameterType != typeof(MethodBase))
            throw new InvalidOperationException("Harmony.Patch selection chose the wrong overload.");

        MethodInfo missingPostfix = (MethodInfo)finder.Invoke(null, new object[] { typeof(MissingPostfixHost), typeof(FakeHarmonyMethod) });
        if (missingPostfix != null)
            throw new InvalidOperationException("Harmony.Patch discovery must fail closed when the postfix parameter is absent.");

        MethodInfo ambiguous = (MethodInfo)finder.Invoke(null, new object[] { typeof(AmbiguousPatchHost), typeof(FakeHarmonyMethod) });
        if (ambiguous != null)
            throw new InvalidOperationException("Harmony.Patch discovery must fail closed when more than one compatible overload exists.");
    }
}
