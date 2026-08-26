using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class GridWindowSizingRuntime
    {
        const int MaxDeferredAttempts = 30;

        sealed class PendingWindow
        {
            internal readonly WeakReference Window;
            internal int Attempts;
            internal PendingWindow(object window) { Window = new WeakReference(window); }
        }

        internal static Action<string> LogWarning;
        internal static Action RequestFlush;
        internal static Type GridWindowType;
        static readonly List<PendingWindow> PendingWindows = new List<PendingWindow>();

        internal static bool HasPending => PendingWindows.Count != 0;

        internal static void ObserveItemUiContext(object itemUiContext)
        {
            if (itemUiContext == null || GridWindowType == null) return;
            try
            {
                IList windows = ReflectionTools.ReadMember(itemUiContext, "_windows") as IList;
                if (windows == null || windows.Count == 0) return;

                object windowData = windows[windows.Count - 1];
                if (windowData == null) return;
                object windowType = ReflectionTools.ReadMember(windowData, "WindowType");
                if (!Equals(windowType, GridWindowType)) return;

                object item = ReflectionTools.ReadMember(windowData, "Item");
                if (!IsRuntimeCandidate(item)) return;

                object window = ReflectionTools.ReadMember(windowData, "Window");
                if (window == null || !GridWindowType.IsInstanceOfType(window)) return;
                ObserveWindow(window);
            }
            catch (Exception exception)
            {
                LogWarning?.Invoke("B&A&HB compact window observation failed closed: " + Unwrap(exception).GetType().FullName + ": " + Unwrap(exception).Message);
            }
        }

        static void ObserveWindow(object window)
        {
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

        internal static void Flush()
        {
            if (PendingWindows.Count == 0) return;
            for (int i = 0; i < PendingWindows.Count; i++)
            {
                PendingWindow pending = PendingWindows[i];
                object window = pending.Window.Target;
                if (window == null || TryAdjust(window) || ++pending.Attempts >= MaxDeferredAttempts)
                    PendingWindows.RemoveAt(i--);
            }
        }

        static bool TryAdjust(object window)
        {
            Component component = window as Component;
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy) return false;

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
                LogWarning?.Invoke("Could not apply compact ArmBand GridWindow layout element: " + Unwrap(exception).Message);
            }
        }

        static void SetFloat(object target, string propertyName, float value)
        {
            PropertyInfo property = ReflectionTools.FindInstanceProperty(target?.GetType(), propertyName, typeof(float));
            if (property != null && property.CanWrite)
            {
                try { property.SetValue(target, value, null); }
                catch (Exception exception) { LogWarning?.Invoke("B&A&HB compact layout property failed closed: " + propertyName + ": " + Unwrap(exception).Message); }
            }
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }

        internal static void Reset()
        {
            PendingWindows.Clear();
            LogWarning = null;
            RequestFlush = null;
            GridWindowType = null;
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
                Type itemUiContextType = ReflectionTools.FindType("EFT.UI.ItemUiContext");
                Type gridWindowType = ReflectionTools.FindType("EFT.UI.GridWindow");
                Type compoundItemType = ReflectionTools.FindType("EFT.InventoryLogic.CompoundItem");
                Type itemContextType = ReflectionTools.FindType("EFT.InventoryLogic.ItemContext");
                if (harmonyType == null || harmonyMethodType == null || itemUiContextType == null || gridWindowType == null || compoundItemType == null || itemContextType == null)
                    return Fail("SPT 4.1 ItemUiContext.OpenItem/GridWindow boundary was not found; compact ArmBand window sizing is disabled.");

                MethodInfo openItem = ReflectionTools.FindInstanceMethod(itemUiContextType, "OpenItem", typeof(void), compoundItemType, itemContextType);
                if (openItem == null)
                    return Fail("SPT 4.1 ItemUiContext.OpenItem(CompoundItem, ItemContext) boundary was not found; compact ArmBand window sizing is disabled.");

                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (!HarmonyInstallPolicy.CanBegin(true, patchMethod != null, harmonyMethodConstructor != null, unpatchSelf != null))
                    return Fail("Harmony patch API is incompatible; compact ArmBand window sizing is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                if (harmony == null) return Fail("Harmony instance creation failed; compact ArmBand window sizing is disabled.");

                GridWindowSizingRuntime.LogWarning = logWarning;
                GridWindowSizingRuntime.GridWindowType = gridWindowType;
                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, openItem, postfix);

                logInfo?.Invoke("B&A&HB compact ArmBand GridWindow sizing installed on ItemUiContext.OpenItem(CompoundItem, ItemContext).");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Compact ArmBand GridWindow sizing installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo originalMethod = original as MethodInfo;
            if (originalMethod == null || originalMethod.DeclaringType == null) return null;
            DynamicMethod postfix = new DynamicMethod(
                "CompactArmBandOpenItemPostfix",
                typeof(void),
                new[] { originalMethod.DeclaringType },
                typeof(GridWindowSizingPatches),
                true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__instance");
            ILGenerator il = postfix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(GridWindowSizingRuntime).GetMethod(nameof(GridWindowSizingRuntime.ObserveItemUiContext), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return postfix;
        }

        static MethodInfo Method(string name)
        {
            MethodInfo[] methods = typeof(GridWindowSizingPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++) if (string.Equals(methods[i].Name, name, StringComparison.Ordinal)) return methods[i];
            return null;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
                if (string.Equals(methods[i].Name, name, StringComparison.Ordinal) && methods[i].GetParameters().Length == 0) return methods[i];
            return null;
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
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }

        bool Fail(string message) { logWarning?.Invoke(message); return false; }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null;
            unpatchSelf = null;
            GridWindowSizingRuntime.Reset();
        }
    }
}
