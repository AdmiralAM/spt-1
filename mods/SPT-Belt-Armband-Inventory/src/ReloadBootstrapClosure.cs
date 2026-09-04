using System;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Bootstrap closure for the two older reload publication guards whose own module
    /// initializers probe Harmony before subscribing to AssemblyLoad. This closure
    /// subscribes first, then invokes their exact private zero-argument TryInstall
    /// contracts. A 0Harmony load in either legacy probe/subscription window is therefore
    /// still observed by this independent handler. It does not patch reload methods,
    /// discover candidates, query inventory, or alter vanilla-first semantics.
    /// </summary>
    internal static class ReloadBootstrapClosure
    {
        static readonly Type[] Targets =
        {
            typeof(ReloadCallerPublicationFence),
            typeof(ReloadEpochPublicationFence)
        };

        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            if (!SweepNeedsFutureRetry())
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        }

        static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (!SweepNeedsFutureRetry())
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        }

        static bool SweepNeedsFutureRetry()
        {
            bool retry = false;
            for (int i = 0; i < Targets.Length; i++)
            {
                if (TryInstallTarget(Targets[i], out bool terminal))
                    continue;
                if (!terminal)
                    retry = true;
            }
            return retry;
        }

        static bool TryInstallTarget(Type target, out bool terminal)
        {
            terminal = true;
            if (target == null) return false;

            try
            {
                MethodInfo install = ResolveTryInstall(target);
                FieldInfo terminalField = ResolveTerminalFailure(target);
                if (install == null || terminalField == null)
                    return false;

                object result = install.Invoke(null, null);
                if (result is bool installed && installed)
                {
                    terminal = false;
                    return true;
                }

                object terminalValue = terminalField.GetValue(null);
                if (!(terminalValue is bool value))
                    return false;
                terminal = value;
                return false;
            }
            catch
            {
                // The target guards retain their own fail-closed lifecycle. A bootstrap
                // reflection failure must not manufacture installation authority here.
                terminal = true;
                return false;
            }
        }

        static MethodInfo ResolveTryInstall(Type target)
        {
            MethodInfo selected = null;
            MethodInfo[] methods = target.GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "TryInstall", StringComparison.Ordinal)
                    || method.ReturnType != typeof(bool)
                    || method.GetParameters().Length != 0)
                    continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static FieldInfo ResolveTerminalFailure(Type target)
        {
            FieldInfo field = target.GetField("terminalFailure", BindingFlags.Static | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(bool) ? field : null;
        }

        internal static bool ShouldRetryTargetForRegression(bool installSucceeded, bool terminal)
        {
            return !installSucceeded && !terminal;
        }

        internal static MethodInfo ResolveTryInstallForRegression(Type target) => ResolveTryInstall(target);
        internal static FieldInfo ResolveTerminalFailureForRegression(Type target) => ResolveTerminalFailure(target);
    }
}
