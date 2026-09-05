using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class CompactFaceHeadBandPresentationRuntime
    {
        const float HeadBandHeight = 44f;
        const float Gap = 4f;

        sealed class LayoutState
        {
            internal readonly WeakReference EquipmentTab;
            internal readonly Vector2 FaceAnchoredPosition;
            internal readonly Vector2 FaceSize;
            internal readonly Vector2 FaceAnchorMin;
            internal readonly Vector2 FaceAnchorMax;
            internal readonly Vector2 FacePivot;

            internal LayoutState(Component equipmentTab, RectTransform faceRect)
            {
                EquipmentTab = new WeakReference(equipmentTab);
                FaceAnchoredPosition = faceRect.anchoredPosition;
                FaceSize = faceRect.rect.size;
                FaceAnchorMin = faceRect.anchorMin;
                FaceAnchorMax = faceRect.anchorMax;
                FacePivot = faceRect.pivot;
            }
        }

        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static FieldInfo EquipmentTabSlotViewsField;
        internal static object FaceCoverSlotKey;
        internal static object HeadBandSlotKey;

        static readonly Dictionary<int, LayoutState> States = new Dictionary<int, LayoutState>();
        static bool proofLogged;
        static bool failureLogged;

        internal static void AfterEquipmentTabShow(object equipmentTabObject)
        {
            Component equipmentTab = equipmentTabObject as Component;
            if (equipmentTab == null || EquipmentTabSlotViewsField == null || FaceCoverSlotKey == null || HeadBandSlotKey == null)
                return;

            try
            {
                IDictionary slotViews = EquipmentTabSlotViewsField.GetValue(equipmentTab) as IDictionary;
                if (slotViews == null || !slotViews.Contains(FaceCoverSlotKey) || !slotViews.Contains(HeadBandSlotKey))
                    return;

                Component faceView = slotViews[FaceCoverSlotKey] as Component;
                Component headBandView = slotViews[HeadBandSlotKey] as Component;
                RectTransform faceRect = faceView == null ? null : faceView.transform as RectTransform;
                RectTransform headBandRect = headBandView == null ? null : headBandView.transform as RectTransform;
                if (faceRect == null || headBandRect == null || faceRect.parent == null)
                    return;

                int key = equipmentTab.GetInstanceID();
                LayoutState state;
                Component stateOwner = null;
                if (States.TryGetValue(key, out state))
                    stateOwner = state.EquipmentTab.Target as Component;

                if (state == null || stateOwner == null || !ReferenceEquals(stateOwner, equipmentTab))
                {
                    state = new LayoutState(equipmentTab, faceRect);
                    States[key] = state;
                }

                Apply(state, faceRect, headBandRect);
            }
            catch (Exception exception)
            {
                FailOnce("Compact Face/HeadBand presentation failed closed", exception);
            }
        }

        static void Apply(LayoutState state, RectTransform faceRect, RectTransform headBandRect)
        {
            float originalHeight = Mathf.Max(1f, state.FaceSize.y);
            float originalWidth = Mathf.Max(1f, state.FaceSize.x);
            float faceHeight = Mathf.Max(44f, Mathf.Floor((originalHeight - Gap) * 0.5f));
            float combinedHeight = HeadBandHeight + Gap + faceHeight;

            // Keep the entire redesign inside the original FaceCover footprint.
            // No host-panel resize/reflow and no movement of neighboring native slots.
            float overflow = combinedHeight - originalHeight;
            if (overflow > 0f)
                faceHeight = Mathf.Max(32f, faceHeight - overflow);

            faceRect.anchorMin = state.FaceAnchorMin;
            faceRect.anchorMax = state.FaceAnchorMax;
            faceRect.pivot = state.FacePivot;
            faceRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, originalWidth);
            faceRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, faceHeight);
            faceRect.anchoredPosition = state.FaceAnchoredPosition - new Vector2(0f, (HeadBandHeight + Gap) * 0.5f);

            if (headBandRect.parent != faceRect.parent)
                headBandRect.SetParent(faceRect.parent, false);
            headBandRect.SetSiblingIndex(faceRect.GetSiblingIndex());
            headBandRect.anchorMin = state.FaceAnchorMin;
            headBandRect.anchorMax = state.FaceAnchorMax;
            headBandRect.pivot = state.FacePivot;
            headBandRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, originalWidth);
            headBandRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, HeadBandHeight);
            headBandRect.anchoredPosition = state.FaceAnchoredPosition + new Vector2(0f, (faceHeight + Gap) * 0.5f);
            headBandRect.gameObject.SetActive(true);

            if (!proofLogged)
            {
                proofLogged = true;
                LogInfo?.Invoke("B&A&HB COMPACT FACE/HEADBAND PROOF: stable reflow suppressed; slot16 and FaceCover share the original FaceCover footprint; face="
                    + originalWidth.ToString("0.0") + "x" + faceHeight.ToString("0.0")
                    + ", headband=" + originalWidth.ToString("0.0") + "x" + HeadBandHeight.ToString("0.0")
                    + ", hostPanelMutation=false.");
            }
        }

        static void FailOnce(string message, Exception exception)
        {
            if (failureLogged) return;
            failureLogged = true;
            Exception root = exception;
            while (root is TargetInvocationException invocation && invocation.InnerException != null)
                root = invocation.InnerException;
            LogWarning?.Invoke(message + ": " + root.GetType().FullName + ": " + root.Message);
        }

        internal static void Reset()
        {
            LogInfo = null;
            LogWarning = null;
            EquipmentTabSlotViewsField = null;
            FaceCoverSlotKey = null;
            HeadBandSlotKey = null;
            States.Clear();
            proofLogged = false;
            failureLogged = false;
        }
    }

    internal sealed class CompactFaceHeadBandPresentationPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.compact-face-headband";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;
        bool ownsPresentation;

        internal CompactFaceHeadBandPresentationPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type equipmentType = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type equipmentSlotType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type equipmentTabType = ReflectionTools.FindType("EFT.UI.EquipmentTab");
                if (harmonyType == null || harmonyMethodType == null || equipmentType == null || equipmentSlotType == null || equipmentTabType == null)
                    return Fail("Compact Face/HeadBand boundary missing; accepted stable presentation remains active.");

                MethodInfo show = FindEquipmentTabShow(equipmentTabType, equipmentType);
                FieldInfo slotViewsField = FindFieldInHierarchy(equipmentTabType, "_slotViews", typeof(IDictionary));
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (show == null || slotViewsField == null || patchMethod == null || harmonyMethodConstructor == null || unpatchSelf == null)
                    return Fail("Compact Face/HeadBand exact EquipmentTab.Show contract changed; accepted stable presentation remains active.");

                object faceCoverKey;
                try { faceCoverKey = Enum.Parse(equipmentSlotType, "FaceCover", false); }
                catch { return Fail("FaceCover equipment-slot identity could not be resolved; accepted stable presentation remains active."); }

                CompactFaceHeadBandPresentationRuntime.LogInfo = logInfo;
                CompactFaceHeadBandPresentationRuntime.LogWarning = logWarning;
                CompactFaceHeadBandPresentationRuntime.EquipmentTabSlotViewsField = slotViewsField;
                CompactFaceHeadBandPresentationRuntime.FaceCoverSlotKey = faceCoverKey;
                CompactFaceHeadBandPresentationRuntime.HeadBandSlotKey = Enum.ToObject(equipmentSlotType, RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, show, postfix);

                // Switch presentation ownership only after the new exact patch has
                // installed. If anything above fails, Stable Baseline 1 remains the
                // active fallback without any behavioral change.
                HeadBandRenderSettle.Reset();
                HeadBandRenderSettle.Suppressed = true;
                ownsPresentation = true;

                logInfo?.Invoke("B&A&HB compact Face/HeadBand presentation installed as an EquipmentTab.Show postfix; stable 48px reflow is suppressed while this owner is active.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Compact Face/HeadBand installation failed safely: " + exception.GetType().FullName + ": " + exception.Message);
            }
        }

        static MethodInfo FindEquipmentTabShow(Type equipmentTabType, Type equipmentType)
        {
            MethodInfo selected = null;
            MethodInfo[] methods = equipmentTabType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Show", StringComparison.Ordinal) || method.ReturnType != typeof(void)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 6 || parameters[1].ParameterType != equipmentType || parameters[5].ParameterType != typeof(bool)) continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static FieldInfo FindFieldInHierarchy(Type type, string name, Type assignableType)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null && (assignableType.IsAssignableFrom(field.FieldType) || field.FieldType.IsAssignableFrom(assignableType))) return field;
            }
            return null;
        }

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo method = original as MethodInfo;
            if (method == null || method.DeclaringType == null) return null;
            DynamicMethod postfix = new DynamicMethod("BAndHBCompactFaceHeadBandPostfix", typeof(void), new[] { method.DeclaringType }, typeof(CompactFaceHeadBandPresentationPatches), true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__instance");
            ILGenerator il = postfix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(CompactFaceHeadBandPresentationRuntime).GetMethod(nameof(CompactFaceHeadBandPresentationRuntime.AfterEquipmentTabShow), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return postfix;
        }

        static MethodInfo Method(string name)
        {
            return typeof(CompactFaceHeadBandPresentationPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
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
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                        return method;
            }
            return null;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
                if (string.Equals(methods[i].Name, name, StringComparison.Ordinal) && methods[i].GetParameters().Length == 0)
                    return methods[i];
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                    args[i] = postfix;
            patchMethod.Invoke(harmony, args);
        }

        bool Fail(string message)
        {
            logWarning?.Invoke(message);
            return false;
        }

        public void Dispose()
        {
            if (ownsPresentation)
            {
                HeadBandRenderSettle.Suppressed = false;
                ownsPresentation = false;
            }
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); }
            catch { }
            harmony = null;
            unpatchSelf = null;
            CompactFaceHeadBandPresentationRuntime.Reset();
        }
    }
}
