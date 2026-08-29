using System;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal static class HeadwearCompatibilityRuntime
    {
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static Func<object, string> ReadSlotId;
        internal static Func<object, string> ReadTemplateId;
        static bool proofLogged;
        static bool failureLogged;

        internal static void AfterCompatibility(object slot, object item, ref bool result)
        {
            if (!result || slot == null || item == null || ReadSlotId == null || ReadTemplateId == null) return;
            try
            {
                if (!DedicatedSlotPresentationPolicy.ShouldSuppressVanillaHeadwearCompatibility(ReadSlotId(slot), ReadTemplateId(item)))
                    return;

                result = false;
                if (!proofLogged)
                {
                    proofLogged = true;
                    LogInfo?.Invoke("B&A&HB HEADWEAR FILTER PROOF: Emergency HeadBand compatibility with vanilla Headwear was suppressed; dedicated slot16 remains the only wearable target.");
                }
            }
            catch (Exception exception)
            {
                if (failureLogged) return;
                failureLogged = true;
                Exception root = Unwrap(exception);
                LogWarning?.Invoke("B&A&HB Headwear compatibility filter failed closed: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        internal static void Reset()
        {
            LogInfo = null;
            LogWarning = null;
            ReadSlotId = null;
            ReadTemplateId = null;
            proofLogged = false;
            failureLogged = false;
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }
    }

    internal sealed class HeadwearCompatibilityPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.headwear-compatibility";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal HeadwearCompatibilityPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type slotType = ReflectionTools.FindType("EFT.InventoryLogic.Slot");
                Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
                if (harmonyType == null || harmonyMethodType == null || slotType == null || itemType == null)
                    return Fail("Headwear compatibility boundary missing; dedicated HeadBand remains functional but vanilla highlight suppression is disabled.");

                MethodInfo target = FindExactInstanceMethod(slotType, "CheckCompatibility", typeof(bool), itemType);
                MemberInfo slotId = FindReadableMember(slotType, "ID");
                MemberInfo templateId = FindReadableMember(itemType, "StringTemplateId") ?? FindReadableMember(itemType, "TemplateId");
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (target == null || slotId == null || templateId == null || patchMethod == null || hmCtor == null || unpatchSelf == null)
                    return Fail("Exact Slot.CheckCompatibility/item identity boundary changed; false Headwear highlight suppression is disabled.");

                Func<object, string> readSlotId = BuildStringReader(slotType, slotId);
                Func<object, string> readTemplateId = BuildStringReader(itemType, templateId);
                if (readSlotId == null || readTemplateId == null)
                    return Fail("Headwear compatibility delegates could not be startup-bound safely.");

                HeadwearCompatibilityRuntime.LogInfo = logInfo;
                HeadwearCompatibilityRuntime.LogWarning = logWarning;
                HeadwearCompatibilityRuntime.ReadSlotId = readSlotId;
                HeadwearCompatibilityRuntime.ReadTemplateId = readTemplateId;

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object postfix = hmCtor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, target, postfix);
                logInfo?.Invoke("B&A&HB exact Emergency HeadBand -> vanilla Headwear compatibility suppression installed with startup-bound delegates.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Headwear compatibility installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static MethodInfo FindExactInstanceMethod(Type type, string name, Type returnType, params Type[] parameterTypes)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo[] methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, name, StringComparison.Ordinal) || method.ReturnType != returnType) continue;
                    ParameterInfo[] actual = method.GetParameters();
                    if (actual.Length != parameterTypes.Length) continue;
                    bool match = true;
                    for (int p = 0; p < actual.Length; p++) if (actual[p].ParameterType != parameterTypes[p]) { match = false; break; }
                    if (match) return method;
                }
            }
            return null;
        }

        static MemberInfo FindReadableMember(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0 && property.GetGetMethod(true) != null) return property;
                FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        static Func<object, string> BuildStringReader(Type ownerType, MemberInfo member)
        {
            Type valueType = member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;
            DynamicMethod dm = new DynamicMethod("BAndHB_ReadCompatibilityIdentity", typeof(string), new[] { typeof(object) }, typeof(HeadwearCompatibilityPatches), true);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, ownerType);
            if (member is PropertyInfo p) il.Emit(OpCodes.Callvirt, p.GetGetMethod(true));
            else il.Emit(OpCodes.Ldfld, (FieldInfo)member);
            if (valueType != typeof(string))
            {
                if (valueType.IsValueType) il.Emit(OpCodes.Box, valueType);
                il.Emit(OpCodes.Callvirt, typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes));
            }
            il.Emit(OpCodes.Ret);
            return (Func<object, string>)dm.CreateDelegate(typeof(Func<object, string>));
        }

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo method = original as MethodInfo;
            if (method == null || method.DeclaringType == null) return null;
            ParameterInfo[] p = method.GetParameters();
            if (p.Length != 1) return null;

            DynamicMethod postfix = new DynamicMethod(
                "BAndHBHeadwearCompatibilityPostfix",
                typeof(void),
                new[] { method.DeclaringType, p[0].ParameterType, typeof(bool).MakeByRefType() },
                typeof(HeadwearCompatibilityPatches),
                true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__instance");
            postfix.DefineParameter(2, ParameterAttributes.None, "__0");
            postfix.DefineParameter(3, ParameterAttributes.Out, "__result");
            ILGenerator il = postfix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, typeof(HeadwearCompatibilityRuntime).GetMethod(nameof(HeadwearCompatibilityRuntime.AfterCompatibility), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return postfix;
        }

        static MethodInfo Method(string name)
        {
            MethodInfo[] methods = typeof(HeadwearCompatibilityPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
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
            HeadwearCompatibilityRuntime.Reset();
        }
    }
}
