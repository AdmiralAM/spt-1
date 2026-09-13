using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Admiral.SecondLife.Client
{
    internal sealed class RecoveryRuntimeContract
    {
        internal Type LocalGameType { get; private set; }
        internal MethodInfo CreateCorpse { get; private set; }
        internal MethodInfo InitiateGameStopping { get; private set; }

        internal static bool TryResolve(out RecoveryRuntimeContract contract, out string failure)
        {
            contract = null;
            failure = null;

            Type player = AccessTools.TypeByName("EFT.Player");
            Type localPlayer = AccessTools.TypeByName("EFT.LocalPlayer");
            Type localGame = AccessTools.TypeByName("EFT.LocalGame");
            Type profile = AccessTools.TypeByName("EFT.Profile");
            Type inventory = AccessTools.TypeByName("EFT.InventoryLogic.Inventory");
            Type equipment = AccessTools.TypeByName("EFT.InventoryLogic.InventoryEquipment");
            Type equipmentTemplate = AccessTools.TypeByName("EFT.InventoryLogic.InventoryEquipmentTemplate");
            if (new[] { player, localPlayer, localGame, profile, inventory, equipment, equipmentTemplate }.Any(type => type == null))
                return Fail("required SPT 4.1 recovery types are missing", out failure);

            MethodInfo createCorpse = UniqueMethod(player, "CreateCorpse", isStatic: false, parameterCount: 0);
            MethodInfo initiateGameStopping = localGame.BaseType?.GetMethod(
                "InitiateGameStopping",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            FieldInfo profileInventory = profile.GetField("Inventory", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo inventoryEquipment = inventory.GetField("Equipment", BindingFlags.Instance | BindingFlags.Public);
            ConstructorInfo equipmentConstructor = equipment.GetConstructor(new[] { typeof(string), equipmentTemplate });
            ConstructorInfo inventoryConstructor = inventory.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(ctor => ctor.GetParameters().Length == 12 && ctor.GetParameters()[0].ParameterType == equipment);
            MethodInfo localPlayerCreate = UniqueMethod(localPlayer, "Create", isStatic: true, parameterCount: 21);
            Type inventoryController = player.GetNestedType("SinglePlayerInventoryController", BindingFlags.Public | BindingFlags.NonPublic);
            ConstructorInfo inventoryControllerConstructor = inventoryController?.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(ctor => ctor.GetParameters().Length == 4 && ctor.GetParameters()[0].ParameterType == player && ctor.GetParameters()[1].ParameterType == profile);

            if (createCorpse == null || initiateGameStopping == null)
                return Fail("exact corpse/finalization boundary changed", out failure);
            if (profileInventory == null || profileInventory.IsInitOnly || profileInventory.FieldType != inventory)
                return Fail("Profile.Inventory is no longer a replaceable Inventory field", out failure);
            if (inventoryEquipment == null || !inventoryEquipment.IsInitOnly || inventoryEquipment.FieldType != equipment)
                return Fail("Inventory.Equipment ownership shape changed", out failure);
            if (equipmentConstructor == null || inventoryConstructor == null)
                return Fail("empty equipment/inventory construction signatures changed", out failure);
            if (localPlayerCreate == null || inventoryControllerConstructor == null)
                return Fail("local-player reconstruction signatures changed", out failure);

            contract = new RecoveryRuntimeContract
            {
                LocalGameType = localGame,
                CreateCorpse = createCorpse,
                InitiateGameStopping = initiateGameStopping
            };
            return true;
        }

        static MethodInfo UniqueMethod(Type type, string name, bool isStatic, int parameterCount) =>
            type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method =>
                    method.Name == name &&
                    method.IsStatic == isStatic &&
                    !method.IsGenericMethod &&
                    method.GetParameters().Length == parameterCount);

        static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
