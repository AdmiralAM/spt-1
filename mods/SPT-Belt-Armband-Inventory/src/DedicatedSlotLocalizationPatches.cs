using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class DedicatedSlotLocalizationRuntime
    {
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static FieldInfo HeaderTextField;

        static readonly Dictionary<int, DedicatedView> DedicatedViews = new Dictionary<int, DedicatedView>();
        static bool? russianUi;
        static bool proofLogged;

        internal static void AfterSlotShow(object slotView, object slot)
        {
            if (slotView == null || slot == null || HeaderTextField == null) return;

            try
            {
                string slotId = ReflectionTools.ReadMember(slot, "ID")?.ToString();
                if (string.Equals(slotId, DedicatedSlotPresentationPolicy.VanillaHeadwearSlotId, StringComparison.Ordinal))
                {
                    russianUi = DedicatedSlotPresentationPolicy.ResolveRussian(
                        russianUi,
                        ReadHeader(slotView),
                        Application.systemLanguage == SystemLanguage.Russian);
                    RelabelKnownViews();
                    return;
                }

                string caption = DedicatedSlotPresentationPolicy.Caption(slotId, IsRussian());
                if (caption == null) return;

                SetHeader(slotView, caption);
                Component component = slotView as Component;
                if (component != null)
                    DedicatedViews[component.GetInstanceID()] = new DedicatedView(component, slotId);

                if (!proofLogged)
                {
                    proofLogged = true;
                    LogInfo?.Invoke("B&A&HB DEDICATED LABEL PROOF: native dedicated SlotView captions are localized from exact slot IDs; language=" + (IsRussian() ? "ru" : "en") + ".");
                }
            }
            catch (Exception exception)
            {
                Exception root = Unwrap(exception);
                LogWarning?.Invoke("B&A&HB dedicated-slot localization failed closed: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static bool IsRussian()
        {
            return russianUi ?? Application.systemLanguage == SystemLanguage.Russian;
        }

        static void RelabelKnownViews()
        {
            bool russian = IsRussian();
            foreach (DedicatedView view in DedicatedViews.Values)
            {
                if (view.Component == null) continue;
                string caption = DedicatedSlotPresentationPolicy.Caption(view.SlotId, russian);
                if (caption != null) SetHeader(view.Component, caption);
            }
        }

        static string ReadHeader(object slotView)
        {
            object header = HeaderTextField.GetValue(slotView);
            if (header == null) return null;
            PropertyInfo property = ReflectionTools.FindInstanceProperty(header.GetType(), "text", typeof(string));
            return property?.GetValue(header, null) as string;
        }

        static void SetHeader(object slotView, string text)
        {
            object header = HeaderTextField.GetValue(slotView);
            if (header == null) return;
            PropertyInfo property = ReflectionTools.FindInstanceProperty(header.GetType(), "text", typeof(string));
            if (property != null && property.CanWrite) property.SetValue(header, text, null);
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }

        internal static void Reset()
        {
            LogInfo = null;
            LogWarning = null;
            HeaderTextField = null;
            DedicatedViews.Clear();
            russianUi = null;
            proofLogged = false;
        }

        readonly struct DedicatedView
        {
            internal readonly Component Component;
            internal readonly string SlotId;

            internal DedicatedView(Component component, string slotId)
            {
                Component = component;
                SlotId = slotId;
            }
        }
    }

    internal sealed class DedicatedSlotLocalizationPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.dedicated-slot-localization";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal DedicatedSlotLocalizationPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type slotViewType = ReflectionTools.FindType("EFT.UI.DragAndDrop.SlotView");
                Type slotType = ReflectionTools.FindType("EFT.InventoryLogic.Slot");
                if (harmonyType == null || harmonyMethodType == null || slotViewType == null || slotType == null)
                    return Fail("Dedicated slot localization boundary missing; native captions remain available but may use fallback text.");

                MethodInfo show = FindSlotViewShow(slotViewType, slotType);
                FieldInfo header = FindNamedField(slotViewType, "_headerText");
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (show == null || header == null || patchMethod == null || hmCtor == null || unpatchSelf == null)
                    return Fail("Exact SlotView.Show/header localization contract changed; localization patch disabled.");

                DedicatedSlotLocalizationRuntime.LogInfo = logInfo;
                DedicatedSlotLocalizationRuntime.LogWarning = logWarning;
                DedicatedSlotLocalizationRuntime.HeaderTextField = header;

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object postfix = hmCtor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, show, postfix);
                logInfo?.Invoke("B&A&HB dedicated EN/RU slot-caption localization installed on exact SlotView.Show; localization owns captions only and does not mutate HeadBand or Gear Panel geometry.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Dedicated slot localization installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static MethodInfo FindSlotViewShow(Type slotViewType, Type slotType)
        {
            MethodInfo[] methods = slotViewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Show", StringComparison.Ordinal) || method.ReturnType != typeof(void)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 7 && parameters[0].ParameterType == slotType) return method;
            }
            return null;
        }

        static FieldInfo FindNamedField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo method = original as MethodInfo;
            if (method == null || method.DeclaringType == null) return null;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0) return null;

            DynamicMethod postfix = new DynamicMethod(
                "BAndHBDedicatedSlotLocalizationPostfix",
                typeof(void),
                new[] { method.DeclaringType, parameters[0].ParameterType },
                typeof(DedicatedSlotLocalizationPatches),
                true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__instance");
            postfix.DefineParameter(2, ParameterAttributes.None, "__0");
            ILGenerator il = postfix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(DedicatedSlotLocalizationRuntime).GetMethod(nameof(DedicatedSlotLocalizationRuntime.AfterSlotShow), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return postfix;
        }

        static MethodInfo Method(string name)
        {
            MethodInfo[] methods = typeof(DedicatedSlotLocalizationPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
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
                if (!string.Equals(method.Name, "Patch", StringComparison.Ordinal)) continue;
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
            DedicatedSlotLocalizationRuntime.Reset();
        }
    }
}
