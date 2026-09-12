using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// InventoryEquipment sizes its integer-indexed slot cache from Slots.Length.
    /// Companion mode deliberately leaves slot15 to Pack 'n' Strap while B&A
    /// publishes HeadBand as slot16, so the sparse sequence needs capacity 17.
    /// </summary>
    internal sealed class EquipmentCacheCapacityPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.equipment-cache-capacity";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        Harmony harmony;

        internal EquipmentCacheCapacityPatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                Type equipment = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type template = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipmentTemplate");
                Type slot = ReflectionTools.FindType("EFT.InventoryLogic.Slot");
                ConstructorInfo target = equipment?.GetConstructor(new[] { typeof(string), template });
                if (target == null || slot == null)
                    throw new InvalidOperationException("exact InventoryEquipment(string, InventoryEquipmentTemplate) boundary missing");

                TargetSlotType = slot;
                harmony = new Harmony(HarmonyId);
                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(EquipmentCacheCapacityPatches), nameof(Transpiler)));
                logInfo?.Invoke("B&A&HB equipment cache capacity installed for sparse HeadBand slot16; minimum cache length=17.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                logWarning?.Invoke("B&A&HB equipment cache capacity install failed: " + exception.GetType().FullName + ": " + exception.Message);
                return false;
            }
        }

        static Type TargetSlotType;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
        {
            List<CodeInstruction> code = input.ToList();
            MethodInfo capacity = AccessTools.Method(typeof(EquipmentCacheCapacityPatches), nameof(RequiredCapacity));
            int matches = 0;
            for (int i = 1; i + 2 < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Ldlen
                    || code[i + 1].opcode != OpCodes.Conv_I4
                    || code[i + 2].opcode != OpCodes.Newarr
                    || !Equals(code[i + 2].operand, TargetSlotType)
                    || code[i - 1].opcode != OpCodes.Ldfld)
                    continue;

                code[i].opcode = OpCodes.Call;
                code[i].operand = capacity;
                matches++;
            }

            if (matches != 1)
                throw new InvalidOperationException("InventoryEquipment slot-cache allocation shape changed; expected exactly one Slot[] allocation from Slots.Length, found " + matches);
            return code;
        }

        static int RequiredCapacity(Array slots)
        {
            return EquipmentCacheCapacityPolicy.RequiredCapacity(
                slots?.Length ?? 0,
                RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);
        }

        public void Dispose()
        {
            try { harmony?.UnpatchSelf(); } catch { }
            harmony = null;
            TargetSlotType = null;
        }
    }

}
