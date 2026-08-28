using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class FirstOpenHeadBandLayoutRuntime
    {
        const int MaxDeferredAttempts = 6;
        const float HeadBandCompactHeight = 44f;
        const float HeadBandGap = 4f;

        sealed class PendingLayout
        {
            internal readonly WeakReference HeadwearView;
            internal int Attempts;

            internal PendingLayout(Component headwearView)
            {
                HeadwearView = new WeakReference(headwearView);
            }
        }

        internal static Action RequestFlush;
        internal static Action<string> LogInfo;
        internal static FieldInfo EquipmentTabSlotViewsField;
        internal static Type EquipmentTabType;
        internal static object HeadBandSlotKey;

        static readonly List<PendingLayout> Pending = new List<PendingLayout>();
        static bool proofLogged;

        internal static bool HasPending => Pending.Count != 0;

        internal static void AfterSlotShow(object slotView, object slot)
        {
            if (slotView == null || slot == null || EquipmentTabType == null
                || EquipmentTabSlotViewsField == null || HeadBandSlotKey == null)
                return;

            string id = ReflectionTools.ReadMember(slot, "ID")?.ToString();
            if (!string.Equals(id, DedicatedSlotPresentationPolicy.VanillaHeadwearSlotId, StringComparison.Ordinal))
                return;

            Component headwearView = slotView as Component;
            if (headwearView == null || headwearView.transform == null) return;

            for (int i = 0; i < Pending.Count; i++)
            {
                Component existing = Pending[i].HeadwearView.Target as Component;
                if (existing == null)
                {
                    Pending.RemoveAt(i--);
                    continue;
                }
                if (ReferenceEquals(existing, headwearView))
                {
                    RequestFlush?.Invoke();
                    return;
                }
            }

            // SlotView.Show fires before the first Items-tab layout has necessarily
            // completed all of EFT's late RectTransform writes. Re-apply only for a
            // short event-triggered settle window; no idle Update/polling loop.
            TryApply(headwearView);
            Pending.Add(new PendingLayout(headwearView));
            RequestFlush?.Invoke();
        }

        internal static void Flush()
        {
            for (int i = 0; i < Pending.Count; i++)
            {
                PendingLayout pending = Pending[i];
                Component headwearView = pending.HeadwearView.Target as Component;
                if (headwearView == null)
                {
                    Pending.RemoveAt(i--);
                    continue;
                }

                bool applied = TryApply(headwearView);
                pending.Attempts++;
                if (pending.Attempts < MaxDeferredAttempts) continue;

                if (applied && !proofLogged)
                {
                    proofLogged = true;
                    LogInfo?.Invoke("B&A&HB FIRST-OPEN LAYOUT PROOF: HeadBand slot16 position survived bounded post-Show settle without tab switching; passes=" + MaxDeferredAttempts + ".");
                }
                Pending.RemoveAt(i--);
            }
        }

        static bool TryApply(Component headwearView)
        {
            if (headwearView == null || headwearView.transform == null || headwearView.transform.parent == null)
                return false;

            Component equipmentTab = headwearView.GetComponentInParent(EquipmentTabType);
            IDictionary slotViews = equipmentTab == null ? null : EquipmentTabSlotViewsField.GetValue(equipmentTab) as IDictionary;
            Component headBandView = slotViews != null && slotViews.Contains(HeadBandSlotKey)
                ? slotViews[HeadBandSlotKey] as Component
                : null;
            if (headBandView == null || headBandView.transform == null) return false;

            RectTransform headBandRect = headBandView.transform as RectTransform;
            RectTransform headwearRect = headwearView.transform as RectTransform;
            if (headBandRect == null || headwearRect == null) return false;

            float headwearHeight = Mathf.Max(1f, headwearRect.rect.height);
            float width = Mathf.Max(1f, headwearRect.rect.width);

            headBandView.transform.SetParent(headwearView.transform.parent, false);
            headBandView.transform.SetSiblingIndex(headwearView.transform.GetSiblingIndex());
            headBandRect.anchorMin = headwearRect.anchorMin;
            headBandRect.anchorMax = headwearRect.anchorMax;
            headBandRect.pivot = headwearRect.pivot;
            headBandRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            headBandRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, HeadBandCompactHeight);
            headBandRect.anchoredPosition = headwearRect.anchoredPosition
                + new Vector2(0f, (headwearHeight + HeadBandCompactHeight) * 0.5f + HeadBandGap);
            headBandView.gameObject.SetActive(true);
            return true;
        }

        internal static void Reset()
        {
            RequestFlush = null;
            LogInfo = null;
            EquipmentTabSlotViewsField = null;
            EquipmentTabType = null;
            HeadBandSlotKey = null;
            Pending.Clear();
            proofLogged = false;
        }
    }

    internal sealed class FirstOpenHeadBandLayoutPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.headband-first-open-layout";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal FirstOpenHeadBandLayoutPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type equipmentSlotType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type equipmentTabType = ReflectionTools.FindType("EFT.UI.EquipmentTab");
                if (harmonyType == null || harmonyMethodType == null || slotViewType == null
                    || slotType == null || equipmentSlotType == null || equipmentTabType == null)
                    return Fail("First-open HeadBand layout boundary missing; native presentation remains active without settle repair.");

                MethodInfo show = FindSlotViewShow(slotViewType, slotType);
                FieldInfo slotViewsField = FindFieldInHierarchy(equipmentTabType, "_slotViews", typeof(IDictionary));
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (show == null || slotViewsField == null || patchMethod == null
                    || harmonyMethodConstructor == null || unpatchSelf == null)
                    return Fail("First-open HeadBand exact SlotView.Show/EquipmentTab map contract changed; settle repair disabled.");

                FirstOpenHeadBandLayoutRuntime.LogInfo = logInfo;
                FirstOpenHeadBandLayoutRuntime.EquipmentTabSlotViewsField = slotViewsField;
                FirstOpenHeadBandLayoutRuntime.EquipmentTabType = equipmentTabType;
                FirstOpenHeadBandLayoutRuntime.HeadBandSlotKey = Enum.ToObject(
                    equipmentSlotType,
                    RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object postfix = harmonyMethodConstructor.Invoke(new object[] { PostfixFactory(show) });
                Patch(patchMethod, harmonyMethodType, show, postfix);
                logInfo?.Invoke("B&A&HB first-open HeadBand layout settle installed on native SlotView.Show; bounded six-pass post-Show repair, no idle polling.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("First-open HeadBand layout installation failed safely: " + root.GetType().FullName + ": " + root.Message);
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

        static FieldInfo FindFieldInHierarchy(Type type, string name, Type assignableType)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null && (assignableType.IsAssignableFrom(field.FieldType) || field.FieldType.IsAssignableFrom(assignableType)))
                    return field;
            }
            return null;
        }

        static MethodInfo PostfixFactory(MethodInfo original)
        {
            ParameterInfo[] parameters = original.GetParameters();
            Type[] signature = new Type[parameters.Length + 1];
            signature[0] = original.DeclaringType;
            for (int i = 0; i < parameters.Length; i++) signature[i + 1] = parameters[i].ParameterType;

            DynamicMethod postfix = new DynamicMethod(
                "BAndHBFirstOpenHeadBandLayoutPostfix",
                typeof(void),
                signature,
                typeof(FirstOpenHeadBandLayoutPatches),
                true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__instance");
            for (int i = 0; i < parameters.Length; i++)
                postfix.DefineParameter(i + 2, ParameterAttributes.None, "__" + i);

            ILGenerator il = postfix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (signature[0].IsValueType) il.Emit(OpCodes.Box, signature[0]);
            il.Emit(OpCodes.Ldarg_1);
            if (signature[1].IsValueType) il.Emit(OpCodes.Box, signature[1]);
            il.Emit(
                OpCodes.Call,
                typeof(FirstOpenHeadBandLayoutRuntime).GetMethod(
                    nameof(FirstOpenHeadBandLayoutRuntime.AfterSlotShow),
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return postfix;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
                if (string.Equals(methods[i].Name, name, StringComparison.Ordinal)
                    && methods[i].GetParameters().Length == 0)
                    return methods[i];
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
                    if (parameters[p].ParameterType == harmonyMethodType
                        && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                        return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType
                    && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                    args[i] = postfix;
            patchMethod.Invoke(harmony, args);
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null)
                current = invocation.InnerException;
            return current;
        }

        bool Fail(string message)
        {
            logWarning?.Invoke(message);
            return false;
        }

        public void Dispose()
        {
            try
            {
                if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null);
            }
            catch { }

            harmony = null;
            unpatchSelf = null;
            FirstOpenHeadBandLayoutRuntime.Reset();
        }
    }
}
