using System;
using System.Linq;
using System.Reflection;

namespace Admiral.SecondLife.Client
{
    internal sealed class RecoveryRuntimeContract
    {
        internal Type LocalGameType { get; private set; }
        internal MethodInfo CreateCorpse { get; private set; }
        internal MethodInfo InitiateGameStopping { get; private set; }
        internal FieldInfo ProfileInventory { get; private set; }
        internal FieldInfo InventoryEquipment { get; private set; }
        internal ConstructorInfo EquipmentConstructor { get; private set; }
        internal ConstructorInfo InventoryConstructor { get; private set; }
        internal FieldInfo GameProfile { get; private set; }
        internal FieldInfo PlayerFactory { get; private set; }
        internal FieldInfo OwnerFactory { get; private set; }
        internal FieldInfo LocalPlayer { get; private set; }
        internal FieldInfo PlayerOwner { get; private set; }
        internal FieldInfo Players { get; private set; }
        internal MethodInfo Spawn { get; private set; }
        internal MethodInfo CreatePlayerCamera { get; private set; }

        internal static bool TryResolve(out RecoveryRuntimeContract contract, out string failure)
        {
            contract = null;
            failure = null;

            Type player = FindType("EFT.Player");
            Type localPlayer = FindType("EFT.LocalPlayer");
            Type localGame = FindType("EFT.LocalGame");
            Type profile = FindType("EFT.Profile");
            Type inventory = FindType("EFT.InventoryLogic.Inventory");
            Type equipment = FindType("EFT.InventoryLogic.InventoryEquipment");
            Type equipmentTemplate = FindType("EFT.InventoryLogic.InventoryEquipmentTemplate");
            Type cameraController = FindType("EFT.CameraControl.PlayerCameraController");
            Type itemUiContext = FindType("EFT.UI.ItemUiContext");
            Type activeHealthController = FindType("EFT.HealthSystem.ActiveHealthController");
            Type healthHelper = FindType("EFT.HealthSystem.HealthHelper");
            Type globalConfiguration = FindType("EFT.GlobalConfiguration");
            Type itemExtensions = FindType("EFT.InventoryLogic.ItemExtensions");
            if (new[] { player, localPlayer, localGame, profile, inventory, equipment, equipmentTemplate, cameraController, itemUiContext, activeHealthController, healthHelper, globalConfiguration, itemExtensions }.Any(type => type == null))
                return Fail("required SPT 4.1 recovery types are missing", out failure);

            Type baseLocalGame = localGame.BaseType;

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
            FieldInfo gameProfile = FindField(baseLocalGame, "_profile");
            FieldInfo playerFactory = FindField(baseLocalGame, "_playerFactory");
            FieldInfo ownerFactory = FindField(baseLocalGame, "_ownerFactory");
            FieldInfo localPlayerField = FindField(baseLocalGame, "_localPlayer");
            FieldInfo playerOwner = FindField(baseLocalGame, "_playerOwner");
            FieldInfo players = FindField(baseLocalGame, "_players");
            MethodInfo spawn = baseLocalGame?.GetMethod("Spawn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo createPlayerCamera = cameraController.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Create" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == player);
            MethodInfo healingConfirmation = itemUiContext.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(method => method.Name == "ShowMessageWindow" && method.GetParameters().Length == 7);
            MethodInfo restoreFullHealth = activeHealthController.GetMethod("RestoreFullHealth", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo realBodyParts = healthHelper.GetField("RealBodyParts", BindingFlags.Static | BindingFlags.Public);
            FieldInfo healthSettings = globalConfiguration.GetField("Health", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo nestedStashItems = itemExtensions.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .SingleOrDefault(method => method.Name == "GetAllItems" && method.GetParameters().Length == 1);

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
            if (new[] { gameProfile, playerFactory, ownerFactory, localPlayerField, playerOwner, players }.Any(field => field == null) ||
                spawn == null || createPlayerCamera == null)
                return Fail("local-game player/owner/camera binding contract changed", out failure);
            if (healingConfirmation == null || restoreFullHealth == null || realBodyParts == null || healthSettings == null || nestedStashItems == null)
                return Fail("native paid-healing contract changed", out failure);

            contract = new RecoveryRuntimeContract
            {
                LocalGameType = localGame,
                CreateCorpse = createCorpse,
                InitiateGameStopping = initiateGameStopping,
                ProfileInventory = profileInventory,
                InventoryEquipment = inventoryEquipment,
                EquipmentConstructor = equipmentConstructor,
                InventoryConstructor = inventoryConstructor,
                GameProfile = gameProfile,
                PlayerFactory = playerFactory,
                OwnerFactory = ownerFactory,
                LocalPlayer = localPlayerField,
                PlayerOwner = playerOwner,
                Players = players,
                Spawn = spawn,
                CreatePlayerCamera = createPlayerCamera
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

        static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        static Type FindType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);

        static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
