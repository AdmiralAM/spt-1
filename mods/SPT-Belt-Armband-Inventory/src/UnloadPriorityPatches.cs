using System;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal sealed class UnloadPriorityPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.unload-priority";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal UnloadPriorityPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type slotEnumType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                if (harmonyType == null || harmonyMethodType == null || equipmentType == null || slotEnumType == null)
                    return Fail("SPT 4.1 inventory types or Harmony were not found; belt unload priority remains disabled.");

                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                if (harmony == null || patchMethod == null || harmonyMethodConstructor == null)
                    return Fail("Harmony patch API is incompatible; belt unload priority remains disabled.");

                if (!UnloadPriorityRuntime.TryInstall(harmony, patchMethod, harmonyMethodType, harmonyMethodConstructor, equipmentType, slotEnumType, logWarning))
                {
                    Dispose();
                    return false;
                }

                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (logInfo != null) logInfo("Belt unload-grid integration installed for GetPrioritizedGridsForUnloadedObject.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Belt unload-priority patch installation failed safely: " + exception.Message);
            }
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

        bool Fail(string message)
        {
            if (logWarning != null) logWarning(message);
            return false;
        }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); }
            catch { }
            harmony = null;
            unpatchSelf = null;
            UnloadPriorityRuntime.Reset();
        }
    }
}
