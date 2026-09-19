using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTItemIntelligence
{
    // Optional bridge: CompatibilityHighlighter keeps ownership of compatibility rules and colors.
    // Item Intelligence only supplies the ItemViews it already tracks in detached container windows.
    public sealed class CompatibilityHighlighterIntegration : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.itemintelligence.compatibilityhighlighter";
        static readonly object sync = new object();
        static readonly HashSet<object> registeredViews = new HashSet<object>(ObjectReferenceComparer.Instance);
        static CompatibilityHighlighterIntegration active;

        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;
        bool disposed;

        internal CompatibilityHighlighterIntegration(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            if (disposed || active != null) return active != null;
            Type driver = FindType("CompatibilityHighlighter.HoverHighlightDriver");
            if (driver == null) return false;
            MethodInfo getViews = driver.GetMethod("GetAllItemViews", BindingFlags.Static | BindingFlags.NonPublic);
            if (getViews == null || !getViews.ReturnType.IsArray) return Unavailable("CompatibilityHighlighter view source was not found; container bridge remains disabled.");

            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                if (harmonyType == null || harmonyMethodType == null) return false;
                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo patchConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (harmony == null || patchMethod == null || patchConstructor == null) return Unavailable("CompatibilityHighlighter bridge could not access Harmony.");

                MethodInfo postfix = BuildPostfix(getViews.ReturnType);
                if (postfix == null) return Unavailable("CompatibilityHighlighter runtime patch method was not finalized by Mono.");
                object harmonyPostfix = patchConstructor.Invoke(new object[] { postfix });
                InvokePatch(patchMethod, getViews, harmonyMethodType, harmonyPostfix);
                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                active = this;
                if (logInfo != null) logInfo("Item Intelligence CompatibilityHighlighter container-window bridge installed.");
                return true;
            }
            catch (Exception exception)
            {
                SafeUnpatch();
                return Unavailable("CompatibilityHighlighter container bridge failed safely: " + Describe(exception));
            }
        }

        internal static void Track(object itemView)
        {
            if (itemView == null) return;
            lock (sync) registeredViews.Add(itemView);
        }

        internal static void Untrack(object itemView)
        {
            if (itemView == null) return;
            lock (sync) registeredViews.Remove(itemView);
        }

        internal static void ClearTracked()
        {
            lock (sync) registeredViews.Clear();
        }

        public static Array MergeRegisteredViews(Array original)
        {
            if (active == null || original == null) return original;
            Type elementType = original.GetType().GetElementType();
            if (elementType == null) return original;
            List<object> merged = new List<object>(original.Length + 16);
            HashSet<object> seen = new HashSet<object>(ObjectReferenceComparer.Instance);
            for (int i = 0; i < original.Length; i++)
            {
                object value = original.GetValue(i);
                if (value != null && seen.Add(value)) merged.Add(value);
            }
            lock (sync)
            {
                foreach (object view in registeredViews)
                {
                    if (view == null || !elementType.IsInstanceOfType(view) || !IsVisible(view) || !seen.Add(view)) continue;
                    merged.Add(view);
                }
            }
            if (merged.Count == original.Length) return original;
            Array result = Array.CreateInstance(elementType, merged.Count);
            for (int i = 0; i < merged.Count; i++) result.SetValue(merged[i], i);
            return result;
        }

        static bool IsVisible(object view)
        {
            try
            {
                Component component = view as Component;
                return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
            }
            catch { return false; }
        }

        static MethodInfo BuildPostfix(Type arrayType)
        {
            AssemblyName assemblyName = new AssemblyName("SPTItemIntelligence.CompatibilityHighlighter.RuntimePatch");
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            ModuleBuilder module = assembly.DefineDynamicModule(assemblyName.Name);
            TypeBuilder type = module.DefineType(
                "SPTItemIntelligence.CompatibilityHighlighterRuntimePatch",
                TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic);
            MethodBuilder method = type.DefineMethod(
                "MergeDetachedContainerViews",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(void),
                new[] { arrayType.MakeByRefType() });
            method.DefineParameter(1, ParameterAttributes.None, "__result");
            MethodInfo merge = typeof(CompatibilityHighlighterIntegration).GetMethod(
                nameof(MergeRegisteredViews), BindingFlags.Static | BindingFlags.Public);
            if (merge == null) return null;
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Call, merge);
            il.Emit(OpCodes.Castclass, arrayType);
            il.Emit(OpCodes.Stind_Ref);
            il.Emit(OpCodes.Ret);
            TypeInfo patchInfo = type.CreateTypeInfo();
            Type patchType = patchInfo == null ? null : patchInfo.AsType();
            if (patchType == null) return null;
            return patchType.GetMethod("MergeDetachedContainerViews", BindingFlags.Static | BindingFlags.Public);
        }

        static string Describe(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException && current.InnerException != null) current = current.InnerException;
            return current.GetType().Name + ": " + current.Message;
        }

        static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            foreach (MethodInfo method in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "Patch") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 3 || !typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType)) continue;
                for (int i = 1; i < parameters.Length; i++)
                    if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void InvokePatch(MethodInfo patchMethod, MethodInfo original, Type harmonyMethodType, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) arguments[i] = postfix;
            patchMethod.Invoke(harmony, arguments);
        }

        bool Unavailable(string message)
        {
            if (logWarning != null) logWarning(message);
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SafeUnpatch();
            ClearTracked();
        }

        void SafeUnpatch()
        {
            if (ReferenceEquals(active, this)) active = null;
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); }
            catch { }
            harmony = null;
            unpatchSelf = null;
        }

        sealed class ObjectReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ObjectReferenceComparer Instance = new ObjectReferenceComparer();
            public new bool Equals(object left, object right) { return ReferenceEquals(left, right); }
            public int GetHashCode(object value) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value); }
        }
    }
}
