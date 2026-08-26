using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class GridWindowSizingRuntime
    {
        sealed class PendingWindow
        {
            internal readonly WeakReference Window;
            internal int Attempts;

            internal PendingWindow(object window)
            {
                Window = new WeakReference(window);
            }
        }

        internal static Action<string> LogWarning;
        internal static Action RequestFlush;
        static readonly List<PendingWindow> PendingWindows = new List<PendingWindow>();

        internal static bool HasPending
        {
            get { return PendingWindows.Count != 0; }
        }

        internal static void Observe(object window, object[] args)
        {
            if (window == null) return;
            if (!ContainsRuntimeCandidate(args) && !IsRuntimeCandidate(ReadWindowItem(window))) return;
            if (TryAdjust(window)) return;

            for (int i = 0; i < PendingWindows.Count; i++)
            {
                object existing = PendingWindows[i].Window.Target;
                if (existing == null)
                {
                    PendingWindows.RemoveAt(i--);
                    continue;
                }
                if (!ReferenceEquals(existing, window)) continue;
                RequestFlush?.Invoke();
                return;
            }

            PendingWindows.Add(new PendingWindow(window));
            RequestFlush?.Invoke();
        }

        static bool ContainsRuntimeCandidate(object[] args)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                object value = args[i];
                if (IsRuntimeCandidate(value)) return true;

                object item = ReflectionTools.ReadMember(value, "Item");
                if (IsRuntimeCandidate(item)) return true;
            }
            return false;
        }

        internal static void Flush()
        {
            if (PendingWindows.Count == 0) return;

            for (int i = 0; i < PendingWindows.Count; i++)
            {
                PendingWindow pending = PendingWindows[i];
                object window = pending.Window.Target;
                if (window == null)
                {
                    PendingWindows.RemoveAt(i--);
                    continue;
                }

                if (TryAdjust(window) || ++pending.Attempts >= 120)
                    PendingWindows.RemoveAt(i--);
            }
        }

        internal static void Reset()
        {
            PendingWindows.Clear();
            LogWarning = null;
            RequestFlush = null;
        }

        static bool TryAdjust(object window)
        {
            Component component = window as Component;
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy) return false;
            if (!IsRuntimeCandidate(ReadWindowItem(window))) return false;

            RectTransform rect = component.transform as RectTransform;
            if (rect == null) return false;

            float width = AccessoryGridPolicy.CompactWindowWidth(RuntimeIdentity.CandidateGridColumns);
            float height = AccessoryGridPolicy.CompactWindowHeight(RuntimeIdentity.CandidateGridRows);
            if (Math.Abs(rect.rect.width - width) < 0.5f && Math.Abs(rect.rect.height - height) < 0.5f) return true;

            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            ApplyLayoutElement(component.gameObject, width, height);
            return true;
        }

        static object ReadWindowItem(object window)
        {
            object item = ReflectionTools.ReadMember(window, "_item");
            if (item != null) return item;

            object context = ReflectionTools.ReadMember(window, "_itemContext");
            item = ReflectionTools.ReadMember(context, "Item");
            if (item != null) return item;

            object source = ReflectionTools.ReadMember(context, "_source");
            return ReflectionTools.ReadMember(source, "Item");
        }

        internal static bool IsRuntimeCandidate(object item)
        {
            if (item == null) return false;
            object stringTemplateId = ReflectionTools.ReadMember(item, "StringTemplateId");
            if (AccessoryGridPolicy.IsRuntimeCandidateTemplate(stringTemplateId as string)) return true;

            object templateId = ReflectionTools.ReadMember(item, "TemplateId");
            return templateId != null && AccessoryGridPolicy.IsRuntimeCandidateTemplate(templateId.ToString());
        }

        static void ApplyLayoutElement(GameObject gameObject, float width, float height)
        {
            try
            {
                Type layoutElementType = Type.GetType("UnityEngine.UI.LayoutElement, UnityEngine.UI", false);
                Component layout = layoutElementType == null ? null : gameObject.GetComponent(layoutElementType);
                if (layout == null) return;

                SetFloat(layout, "minWidth", width);
                SetFloat(layout, "preferredWidth", width);
                SetFloat(layout, "minHeight", height);
                SetFloat(layout, "preferredHeight", height);
            }
            catch (Exception exception)
            {
                if (LogWarning != null) LogWarning("Could not apply compact ArmBand GridWindow layout element: " + exception.Message);
            }
        }

        static void SetFloat(object target, string propertyName, float value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite) property.SetValue(target, value, null);
        }
    }

    internal sealed class GridWindowSizingPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.gridwindow-sizing";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal GridWindowSizingPatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                Type gridWindowType = ReflectionTools.FindType("EFT.UI.GridWindow");
                if (harmonyType == null || harmonyMethodType == null || gridWindowType == null)
                    return Fail("SPT 4.1 GridWindow or Harmony was not found; compact ArmBand window sizing is disabled.");

                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (patchMethod == null || harmonyMethodConstructor == null || unpatchSelf == null)
                    return Fail("Harmony patch API is incompatible; compact ArmBand window sizing is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                int patched = 0;
                MethodInfo[] methods = gridWindowType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!IsGridWindowShow(method)) continue;
                    Patch(patchMethod, harmonyMethodType, method, postfix);
                    patched++;
                }

                if (patched == 0)
                    return Fail("SPT 4.1 GridWindow.Show boundary was not found; compact ArmBand window sizing is disabled.");

                GridWindowSizingRuntime.LogWarning = logWarning;
                if (logInfo != null) logInfo("B&A&HB compact ArmBand GridWindow sizing installed on " + patched + " Show overload(s).");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Compact ArmBand GridWindow sizing installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        internal static bool IsGridWindowShow(MethodInfo method)
        {
            return method != null
                && string.Equals(method.Name, "Show", StringComparison.Ordinal)
                && !method.IsAbstract
                && !method.ContainsGenericParameters
                && method.ReturnType == typeof(void);
        }

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo originalMethod = original as MethodInfo;
            if (originalMethod == null || originalMethod.DeclaringType == null) return null;

            DynamicMethod postfix = new DynamicMethod(
                "CompactArmBandGridWindowPostfix",
                typeof(void),
                new[] { originalMethod.DeclaringType, typeof(object[]) },
                typeof(GridWindowSizingPatches),
                true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__instance");
            postfix.DefineParameter(2, ParameterAttributes.None, "__args");

            ILGenerator il = postfix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(GridWindowSizingRuntime).GetMethod(nameof(GridWindowSizingRuntime.Observe), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return postfix;
        }

        static MethodInfo Method(string name)
        {
            return typeof(GridWindowSizingPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "Patch") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2 || !typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType)) continue;
                for (int p = 1; p < parameters.Length; p++)
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) args[i] = postfix;
            patchMethod.Invoke(harmony, args);
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null)
                current = invocation.InnerException;
            return current;
        }

        bool Fail(string message) { if (logWarning != null) logWarning(message); return false; }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null;
            unpatchSelf = null;
            GridWindowSizingRuntime.Reset();
        }
    }
}
