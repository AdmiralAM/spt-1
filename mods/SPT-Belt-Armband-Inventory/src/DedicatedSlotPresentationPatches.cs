using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class DedicatedSlotPresentationRuntime
    {
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static FieldInfo HeaderTextField;
        static readonly HashSet<int> PositionedHeadBandViews = new HashSet<int>();
        static bool beltLabelProofLogged;
        static bool headBandBindProofLogged;

        internal static void AfterSlotShow(object slotView, object slot)
        {
            if (slotView == null || slot == null || HeaderTextField == null) return;
            try
            {
                string id = ReflectionTools.ReadMember(slot, "ID")?.ToString();
                if (string.Equals(id, RuntimeIdentity.DedicatedBeltWireSlotId, StringComparison.Ordinal))
                {
                    SetHeader(slotView, "BELT");
                    if (!beltLabelProofLogged)
                    {
                        beltLabelProofLogged = true;
                        LogInfo?.Invoke("B&A&HB BELT LABEL PROOF: exact pseudo-slot15 reached SlotView.Show and caption was normalized to BELT.");
                    }
                    return;
                }

                if (!string.Equals(id, RuntimeIdentity.DedicatedHeadBandWireSlotId, StringComparison.Ordinal)) return;

                SetHeader(slotView, "HEADBAND");
                Component component = slotView as Component;
                if (component != null)
                {
                    int instanceId = component.GetInstanceID();
                    if (PositionedHeadBandViews.Add(instanceId))
                    {
                        RectTransform rect = component.transform as RectTransform;
                        if (rect != null)
                        {
                            float height = Mathf.Max(1f, rect.rect.height);
                            rect.anchoredPosition += new Vector2(0f, height + 4f);
                        }
                        else
                        {
                            component.transform.localPosition += new Vector3(0f, 120f, 0f);
                        }
                    }
                    component.gameObject.SetActive(true);
                }

                if (!headBandBindProofLogged)
                {
                    headBandBindProofLogged = true;
                    LogInfo?.Invoke("B&A&HB HEADBAND BIND PROOF: exact pseudo-slot16 reached SlotView.Show; dedicated view caption normalized and moved above Headwear.");
                }
            }
            catch (Exception exception)
            {
                LogWarning?.Invoke("B&A&HB dedicated-slot presentation failed closed: " + Unwrap(exception).GetType().FullName + ": " + Unwrap(exception).Message);
            }
        }

        static void SetHeader(object slotView, string text)
        {
            object header = HeaderTextField.GetValue(slotView);
            if (header == null) return;
            PropertyInfo textProperty = ReflectionTools.FindInstanceProperty(header.GetType(), "text", typeof(string));
            if (textProperty != null && textProperty.CanWrite) textProperty.SetValue(header, text, null);
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
            PositionedHeadBandViews.Clear();
            beltLabelProofLogged = false;
            headBandBindProofLogged = false;
        }
    }

    internal sealed class DedicatedSlotPresentationPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.dedicated-slot-presentation";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal DedicatedSlotPresentationPatches(Action<string> logInfo, Action<string> logWarning)
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
                if (harmonyType == null || harmonyMethodType == null || slotViewType == null)
                    return Fail("Dedicated slot presentation boundary missing; labels/HeadBand placement disabled.");

                MethodInfo show = FindSlotViewShow(slotViewType);
                FieldInfo header = FindNamedField(slotViewType, "_headerText");
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (show == null || header == null || patchMethod == null || harmonyMethodConstructor == null || unpatchSelf == null)
                    return Fail("Exact SlotView.Show/header/Harmony presentation contract changed; presentation patch disabled.");

                DedicatedSlotPresentationRuntime.LogInfo = logInfo;
                DedicatedSlotPresentationRuntime.LogWarning = logWarning;
                DedicatedSlotPresentationRuntime.HeaderTextField = header;

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, show, postfix);
                logInfo?.Invoke("B&A&HB dedicated slot presentation installed on exact SlotView.Show; no polling or scene scan.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Dedicated slot presentation installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static MethodInfo FindSlotViewShow(Type slotViewType)
        {
            MethodInfo[] methods = slotViewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Show", StringComparison.Ordinal) || method.ReturnType != typeof(void)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 7 && string.Equals(parameters[0].ParameterType.FullName, "EFT.InventoryLogic.Slot", StringComparison.Ordinal)) return method;
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
            Type slotType = parameters[0].ParameterType;
            DynamicMethod postfix = new DynamicMethod("BAndHBDedicatedSlotPresentationPostfix", typeof(void), new[] { method.DeclaringType, slotType }, typeof(DedicatedSlotPresentationPatches), true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__instance");
            postfix.DefineParameter(2, ParameterAttributes.None, "__0");
            ILGenerator il = postfix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(DedicatedSlotPresentationRuntime).GetMethod(nameof(DedicatedSlotPresentationRuntime.AfterSlotShow), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return postfix;
        }

        static MethodInfo Method(string name)
        {
            MethodInfo[] methods = typeof(DedicatedSlotPresentationPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
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
            DedicatedSlotPresentationRuntime.Reset();
        }
    }
}
