using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class DedicatedSlotPresentationRuntime
    {
        const string HeadBandCloneName = "B&A&HB HeadBand Slot";

        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static FieldInfo HeaderTextField;
        internal static FieldInfo EquipmentTabSlotViewsField;
        internal static Type EquipmentTabType;
        internal static Type SlotViewType;
        internal static Type EquipmentType;
        internal static object HeadBandSlotKey;
        internal static Func<object, object, object> GetSlot;
        internal static Action<object, object, object, object, object, object, object, object> ShowSlot;

        static readonly Dictionary<int, Component> HeadBandViews = new Dictionary<int, Component>();
        static bool? russianUi;
        static bool beltLabelProofLogged;
        static bool headBandBindProofLogged;
        static bool headBandFailureLogged;

        internal static void AfterSlotShow(
            object slotView,
            object slot,
            object arg1,
            object arg2,
            object arg3,
            object arg4,
            object arg5,
            object arg6)
        {
            if (slotView == null || slot == null || HeaderTextField == null) return;

            try
            {
                string id = ReflectionTools.ReadMember(slot, "ID")?.ToString();
                ObserveLocale(slotView, id);

                if (string.Equals(id, RuntimeIdentity.DedicatedBeltWireSlotId, StringComparison.Ordinal))
                {
                    string caption = DedicatedSlotPresentationPolicy.Caption(id, russianUi == true) ?? "BELT";
                    SetHeader(slotView, caption);
                    if (!beltLabelProofLogged)
                    {
                        beltLabelProofLogged = true;
                        LogInfo?.Invoke("B&A&HB BELT LABEL PROOF: exact pseudo-slot15 reached SlotView.Show; caption=" + caption + ".");
                    }
                    return;
                }

                if (string.Equals(id, RuntimeIdentity.DedicatedHeadBandWireSlotId, StringComparison.Ordinal))
                {
                    string caption = DedicatedSlotPresentationPolicy.Caption(id, russianUi == true) ?? "HEADBAND";
                    SetHeader(slotView, caption);
                    Component dedicatedView = slotView as Component;
                    if (dedicatedView != null) dedicatedView.gameObject.SetActive(true);

                    if (!headBandBindProofLogged)
                    {
                        headBandBindProofLogged = true;
                        LogInfo?.Invoke("B&A&HB HEADBAND BIND PROOF: exact pseudo-slot16 reached native SlotView.Show on a dedicated visible view; caption=" + caption + ".");
                    }
                    return;
                }

                if (!string.Equals(id, DedicatedSlotPresentationPolicy.VanillaHeadwearSlotId, StringComparison.Ordinal)) return;

                BindHeadBandFromHeadwear(slotView, slot, arg1, arg2, arg3, arg4, arg5, arg6);
            }
            catch (Exception exception)
            {
                FailOnce("B&A&HB dedicated-slot presentation failed closed", exception);
            }
        }

        static void ObserveLocale(object slotView, string slotId)
        {
            if (DedicatedWearableSlotContract.IsDedicatedWireSlotId(slotId)) return;
            string text = ReadHeader(slotView);
            if (string.IsNullOrWhiteSpace(text)) return;
            if (DedicatedSlotPresentationPolicy.LooksRussian(text))
            {
                russianUi = true;
                return;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                {
                    if (!russianUi.HasValue) russianUi = false;
                    return;
                }
            }
        }

        static string ReadHeader(object slotView)
        {
            object header = HeaderTextField.GetValue(slotView);
            if (header == null) return null;
            PropertyInfo textProperty = ReflectionTools.FindInstanceProperty(header.GetType(), "text", typeof(string));
            return textProperty != null && textProperty.CanRead ? textProperty.GetValue(header, null) as string : null;
        }

        static void BindHeadBandFromHeadwear(
            object slotView,
            object headwearSlot,
            object arg1,
            object arg2,
            object arg3,
            object arg4,
            object arg5,
            object arg6)
        {
            if (SlotViewType == null
                || EquipmentType == null
                || EquipmentTabType == null
                || EquipmentTabSlotViewsField == null
                || HeadBandSlotKey == null
                || GetSlot == null
                || ShowSlot == null)
                return;

            Component headwearView = slotView as Component;
            if (headwearView == null || headwearView.transform == null || headwearView.transform.parent == null) return;

            object equipment = ReflectionTools.ReadMember(headwearSlot, "ParentItem")
                ?? ReflectionTools.ReadMember(headwearSlot, "Parent");
            if (equipment == null || !EquipmentType.IsInstanceOfType(equipment)) return;

            object headBandSlot = GetSlot(equipment, HeadBandSlotKey);
            if (headBandSlot == null)
            {
                FailOnce("B&A&HB HeadBand native binding could not resolve pseudo-slot16 from InventoryEquipment", null);
                return;
            }

            Component headBandView = GetOrCreateHeadBandView(headwearView);
            if (headBandView == null) return;

            ShowSlot(headBandView, headBandSlot, arg1, arg2, arg3, arg4, arg5, arg6);
            PositionAboveHeadwear(headBandView, headwearView);
            headBandView.gameObject.SetActive(true);
        }

        static Component GetOrCreateHeadBandView(Component headwearView)
        {
            int sourceId = headwearView.GetInstanceID();
            Component cached;
            if (HeadBandViews.TryGetValue(sourceId, out cached) && cached != null)
                return cached;

            Component equipmentTab = headwearView.GetComponentInParent(EquipmentTabType);
            IDictionary slotViews = equipmentTab == null ? null : EquipmentTabSlotViewsField.GetValue(equipmentTab) as IDictionary;

            Component view = null;
            if (slotViews != null && slotViews.Contains(HeadBandSlotKey))
                view = slotViews[HeadBandSlotKey] as Component;

            if (view == null)
            {
                Transform parent = headwearView.transform.parent;
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform child = parent.GetChild(i);
                    if (!string.Equals(child.gameObject.name, HeadBandCloneName, StringComparison.Ordinal)) continue;
                    view = child.gameObject.GetComponent(SlotViewType) as Component;
                    if (view != null) break;
                }
            }

            if (view == null)
            {
                view = UnityEngine.Object.Instantiate(headwearView);
                view.gameObject.name = HeadBandCloneName;
                view.transform.SetParent(headwearView.transform.parent, false);
                view.transform.SetSiblingIndex(headwearView.transform.GetSiblingIndex());
            }

            if (slotViews != null)
            {
                if (slotViews.Contains(HeadBandSlotKey)) slotViews[HeadBandSlotKey] = view;
                else slotViews.Add(HeadBandSlotKey, view);
            }

            HeadBandViews[sourceId] = view;
            return view;
        }

        static void PositionAboveHeadwear(Component headBandView, Component headwearView)
        {
            if (headBandView == null || headwearView == null) return;

            RectTransform headBandRect = headBandView.transform as RectTransform;
            RectTransform headwearRect = headwearView.transform as RectTransform;
            if (headBandRect != null && headwearRect != null)
            {
                float height = Mathf.Max(1f, headwearRect.rect.height);
                headBandRect.anchorMin = headwearRect.anchorMin;
                headBandRect.anchorMax = headwearRect.anchorMax;
                headBandRect.pivot = headwearRect.pivot;
                headBandRect.sizeDelta = headwearRect.sizeDelta;
                headBandRect.anchoredPosition = headwearRect.anchoredPosition + new Vector2(0f, height + 4f);
                return;
            }

            headBandView.transform.localPosition = headwearView.transform.localPosition + new Vector3(0f, 120f, 0f);
        }

        static void SetHeader(object slotView, string text)
        {
            object header = HeaderTextField.GetValue(slotView);
            if (header == null) return;
            PropertyInfo textProperty = ReflectionTools.FindInstanceProperty(header.GetType(), "text", typeof(string));
            if (textProperty != null && textProperty.CanWrite) textProperty.SetValue(header, text, null);
        }

        static void FailOnce(string message, Exception exception)
        {
            if (headBandFailureLogged) return;
            headBandFailureLogged = true;

            if (exception == null)
            {
                LogWarning?.Invoke(message + ".");
                return;
            }

            Exception root = Unwrap(exception);
            LogWarning?.Invoke(message + ": " + root.GetType().FullName + ": " + root.Message);
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
            EquipmentTabSlotViewsField = null;
            EquipmentTabType = null;
            SlotViewType = null;
            EquipmentType = null;
            HeadBandSlotKey = null;
            GetSlot = null;
            ShowSlot = null;
            HeadBandViews.Clear();
            russianUi = null;
            beltLabelProofLogged = false;
            headBandBindProofLogged = false;
            headBandFailureLogged = false;
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
                Type slotType = ReflectionTools.FindType("EFT.InventoryLogic.Slot");
                Type equipmentType = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type equipmentSlotType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type equipmentTabType = ReflectionTools.FindType("EFT.UI.EquipmentTab");
                if (harmonyType == null || harmonyMethodType == null || slotViewType == null || slotType == null
                    || equipmentType == null || equipmentSlotType == null || equipmentTabType == null)
                    return Fail("Dedicated slot native presentation boundary missing; labels/HeadBand binding disabled.");

                MethodInfo show = FindSlotViewShow(slotViewType, slotType);
                FieldInfo header = FindNamedField(slotViewType, "_headerText");
                FieldInfo slotViewsField = FindFieldInHierarchy(equipmentTabType, "_slotViews", typeof(IDictionary));
                MethodInfo getSlot = ReflectionTools.FindInstanceMethod(equipmentType, "GetSlot", slotType, equipmentSlotType);
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (show == null || header == null || slotViewsField == null || getSlot == null
                    || patchMethod == null || harmonyMethodConstructor == null || unpatchSelf == null)
                    return Fail("Exact SlotView.Show/InventoryEquipment.GetSlot/EquipmentTab map contract changed; dedicated presentation disabled.");

                Func<object, object, object> getSlotDelegate = BuildBinaryObjectCall(equipmentType, equipmentSlotType, getSlot);
                Action<object, object, object, object, object, object, object, object> showDelegate = BuildShowCall(slotViewType, show);
                if (getSlotDelegate == null || showDelegate == null)
                    return Fail("Dedicated slot native delegates could not be bound safely.");

                DedicatedSlotPresentationRuntime.LogInfo = logInfo;
                DedicatedSlotPresentationRuntime.LogWarning = logWarning;
                DedicatedSlotPresentationRuntime.HeaderTextField = header;
                DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField = slotViewsField;
                DedicatedSlotPresentationRuntime.EquipmentTabType = equipmentTabType;
                DedicatedSlotPresentationRuntime.SlotViewType = slotViewType;
                DedicatedSlotPresentationRuntime.EquipmentType = equipmentType;
                DedicatedSlotPresentationRuntime.HeadBandSlotKey = Enum.ToObject(
                    equipmentSlotType,
                    RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);
                DedicatedSlotPresentationRuntime.GetSlot = getSlotDelegate;
                DedicatedSlotPresentationRuntime.ShowSlot = showDelegate;

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, show, postfix);
                logInfo?.Invoke("B&A&HB dedicated slot native presentation installed on exact SlotView.Show/GetSlot; event-driven, no polling or scene scan.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Dedicated slot presentation installation failed safely: " + root.GetType().FullName + ": " + root.Message);
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

        static Func<object, object, object> BuildBinaryObjectCall(Type ownerType, Type argumentType, MethodInfo method)
        {
            DynamicMethod dm = new DynamicMethod(
                "BAndHB_GetDedicatedSlot",
                typeof(object),
                new[] { typeof(object), typeof(object) },
                typeof(DedicatedSlotPresentationPatches),
                true);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, ownerType);
            il.Emit(OpCodes.Ldarg_1);
            if (argumentType.IsValueType) il.Emit(OpCodes.Unbox_Any, argumentType);
            else il.Emit(OpCodes.Castclass, argumentType);
            il.Emit(OpCodes.Callvirt, method);
            if (method.ReturnType.IsValueType) il.Emit(OpCodes.Box, method.ReturnType);
            il.Emit(OpCodes.Ret);
            return (Func<object, object, object>)dm.CreateDelegate(typeof(Func<object, object, object>));
        }

        static Action<object, object, object, object, object, object, object, object> BuildShowCall(Type ownerType, MethodInfo method)
        {
            ParameterInfo[] p = method.GetParameters();
            if (p.Length != 7) return null;

            DynamicMethod dm = new DynamicMethod(
                "BAndHB_ShowDedicatedSlot",
                typeof(void),
                new[]
                {
                    typeof(object), typeof(object), typeof(object), typeof(object),
                    typeof(object), typeof(object), typeof(object), typeof(object)
                },
                typeof(DedicatedSlotPresentationPatches),
                true);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, ownerType);
            for (int i = 0; i < p.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, i + 1);
                Type parameterType = p[i].ParameterType;
                if (parameterType.IsValueType) il.Emit(OpCodes.Unbox_Any, parameterType);
                else il.Emit(OpCodes.Castclass, parameterType);
            }
            il.Emit(OpCodes.Callvirt, method);
            il.Emit(OpCodes.Ret);
            return (Action<object, object, object, object, object, object, object, object>)dm.CreateDelegate(
                typeof(Action<object, object, object, object, object, object, object, object>));
        }

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo method = original as MethodInfo;
            if (method == null || method.DeclaringType == null) return null;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 7) return null;

            Type[] signature = new Type[8];
            signature[0] = method.DeclaringType;
            for (int i = 0; i < parameters.Length; i++) signature[i + 1] = parameters[i].ParameterType;

            DynamicMethod postfix = new DynamicMethod(
                "BAndHBDedicatedSlotNativePresentationPostfix",
                typeof(void),
                signature,
                typeof(DedicatedSlotPresentationPatches),
                true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__instance");
            for (int i = 0; i < parameters.Length; i++)
                postfix.DefineParameter(i + 2, ParameterAttributes.None, "__" + i);

            ILGenerator il = postfix.GetILGenerator();
            for (int i = 0; i < signature.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, i);
                if (signature[i].IsValueType) il.Emit(OpCodes.Box, signature[i]);
            }
            il.Emit(
                OpCodes.Call,
                typeof(DedicatedSlotPresentationRuntime).GetMethod(
                    nameof(DedicatedSlotPresentationRuntime.AfterSlotShow),
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return postfix;
        }

        static MethodInfo Method(string name)
        {
            MethodInfo[] methods = typeof(DedicatedSlotPresentationPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++)
                if (string.Equals(methods[i].Name, name, StringComparison.Ordinal)) return methods[i];
            return null;
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
            DedicatedSlotPresentationRuntime.Reset();
        }
    }
}
