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
        // OpenItem is event-driven. A bounded four-frame settle window is enough
        // for Unity/native layout groups to finish without introducing idle polling.
        const int MaxDeferredAttempts = 4;
        const int MaxRecentWindowsToInspect = 4;

        sealed class PendingWindow
        {
            internal readonly WeakReference Window;
            internal readonly int Columns;
            internal readonly int Rows;
            internal int Attempts;

            internal PendingWindow(object window, int columns, int rows)
            {
                Window = new WeakReference(window);
                Columns = columns;
                Rows = rows;
            }
        }

        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static Action RequestFlush;
        internal static Type GridWindowType;
        static readonly List<PendingWindow> PendingWindows = new List<PendingWindow>();
        static readonly HashSet<string> FitProofShapes = new HashSet<string>(StringComparer.Ordinal);

        internal static bool HasPending => PendingWindows.Count != 0;

        internal static void ObserveItemUiContext(object itemUiContext)
        {
            if (itemUiContext == null || GridWindowType == null) return;
            try
            {
                IList windows = ReflectionTools.ReadMember(itemUiContext, "_windows") as IList;
                if (windows == null || windows.Count == 0) return;

                // WindowData.WindowType is not a reliable System.Type discriminator in
                // SPT 4.1.3. RC1 could therefore install successfully yet never resize
                // a physical GridWindow. Resolve the actual Window instance instead.
                int floor = Math.Max(0, windows.Count - MaxRecentWindowsToInspect);
                for (int index = windows.Count - 1; index >= floor; index--)
                {
                    object windowData = windows[index];
                    if (windowData == null) continue;

                    object window = ReflectionTools.ReadMember(windowData, "Window");
                    if (window == null || !GridWindowType.IsInstanceOfType(window)) continue;

                    object item = ReflectionTools.ReadMember(windowData, "Item");
                    if (!TryResolveDescriptor(item, out WearableItemDescriptor descriptor)) continue;

                    ObserveWindow(window, descriptor.GridColumns, descriptor.GridRows);
                    return;
                }
            }
            catch (Exception exception)
            {
                Exception root = Unwrap(exception);
                LogWarning?.Invoke("B&A&HB exact-fit window observation failed closed: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static void ObserveWindow(object window, int columns, int rows)
        {
            TryAdjust(window, columns, rows);

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

            // Even a successful immediate resize is re-applied for a few frames.
            // Native layout may write its preferred size after OpenItem returns.
            PendingWindows.Add(new PendingWindow(window, columns, rows));
            RequestFlush?.Invoke();
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

                TryAdjust(window, pending.Columns, pending.Rows);
                pending.Attempts++;
                if (pending.Attempts >= MaxDeferredAttempts)
                    PendingWindows.RemoveAt(i--);
            }
        }

        static bool TryAdjust(object window, int columns, int rows)
        {
            Component component = window as Component;
            if (component == null || component.gameObject == null || !component.gameObject.activeInHierarchy) return false;

            RectTransform rect = component.transform as RectTransform;
            if (rect == null || rect.rect.width <= 0f || rect.rect.height <= 0f) return false;

            // Geometry comes from the registered item descriptor. No Unity hierarchy
            // scan is needed, and no artificial minimum is allowed: native window
            // chrome + exact declared cell extent only.
            float width = AccessoryGridPolicy.ExactWindowWidth(columns);
            float height = AccessoryGridPolicy.ExactWindowHeight(rows);
            if (width <= 0f || height <= 0f) return false;

            float beforeWidth = rect.rect.width;
            float beforeHeight = rect.rect.height;

            if (Math.Abs(beforeWidth - width) >= 0.5f)
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            if (Math.Abs(beforeHeight - height) >= 0.5f)
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            ApplyLayoutElement(component.gameObject, width, height);

            string shape = columns + "x" + rows;
            if (FitProofShapes.Add(shape))
            {
                LogInfo?.Invoke("B&A&HB WINDOW FIT PROOF: physical GridWindow shape=" + shape
                    + "; before=" + beforeWidth.ToString("0.0") + "x" + beforeHeight.ToString("0.0")
                    + "; target=" + width.ToString("0.0") + "x" + height.ToString("0.0")
                    + "; bounded settle passes=" + MaxDeferredAttempts + ".");
            }
            return true;
        }

        internal static bool TryResolveDescriptor(object item, out WearableItemDescriptor descriptor)
        {
            descriptor = null;
            if (item == null) return false;
            object stringTemplateId = ReflectionTools.ReadMember(item, "StringTemplateId");
            if (stringTemplateId is string direct && WearableItemDescriptorRegistry.TryGet(direct, out descriptor)) return true;
            object templateId = ReflectionTools.ReadMember(item, "TemplateId");
            return templateId != null && WearableItemDescriptorRegistry.TryGet(templateId.ToString(), out descriptor);
        }

        internal static bool IsRuntimeCandidate(object item)
        {
            return TryResolveDescriptor(item, out WearableItemDescriptor descriptor)
                && string.Equals(descriptor.TemplateId, RuntimeIdentity.CandidateItemId, StringComparison.Ordinal);
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
                LogWarning?.Invoke("Could not apply exact-fit wearable GridWindow layout element: " + Unwrap(exception).Message);
            }
        }

        static void SetFloat(object target, string propertyName, float value)
        {
            PropertyInfo property = ReflectionTools.FindInstanceProperty(target?.GetType(), propertyName, typeof(float));
            if (property != null && property.CanWrite)
            {
                try { property.SetValue(target, value, null); }
                catch (Exception exception) { LogWarning?.Invoke("B&A&HB exact-fit layout property failed closed: " + propertyName + ": " + Unwrap(exception).Message); }
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
            FitProofShapes.Clear();
            LogInfo = null;
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
                    return Fail("SPT 4.1 ItemUiContext.OpenItem/GridWindow boundary was not found; exact-fit wearable window sizing is disabled.");

                MethodInfo openItem = ReflectionTools.FindInstanceMethod(itemUiContextType, "OpenItem", typeof(void), compoundItemType, itemContextType);
                if (openItem == null)
                    return Fail("SPT 4.1 ItemUiContext.OpenItem(CompoundItem, ItemContext) boundary was not found; exact-fit wearable window sizing is disabled.");

                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (!HarmonyInstallPolicy.CanBegin(true, patchMethod != null, harmonyMethodConstructor != null, unpatchSelf != null))
                    return Fail("Harmony patch API is incompatible; exact-fit wearable window sizing is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                if (harmony == null) return Fail("Harmony instance creation failed; exact-fit wearable window sizing is disabled.");

                GridWindowSizingRuntime.LogInfo = logInfo;
                GridWindowSizingRuntime.LogWarning = logWarning;
                GridWindowSizingRuntime.GridWindowType = gridWindowType;
                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, openItem, postfix);

                logInfo?.Invoke("B&A&HB exact-fit wearable GridWindow sizing installed on ItemUiContext.OpenItem(CompoundItem, ItemContext); actual GridWindow instance binding with bounded layout settling enabled.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Exact-fit wearable GridWindow sizing installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo originalMethod = original as MethodInfo;
            if (originalMethod == null || originalMethod.DeclaringType == null) return null;
            DynamicMethod postfix = new DynamicMethod(
                "ExactFitWearableOpenItemPostfix",
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
