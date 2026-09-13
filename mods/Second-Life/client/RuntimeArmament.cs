using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Admiral.SecondLife.Client
{
    internal sealed class RuntimeArmament
    {
        internal RuntimeArmament(object pistol, object installedMagazine, object spareMagazine)
        {
            Pistol = pistol;
            InstalledMagazine = installedMagazine;
            SpareMagazine = spareMagazine;
        }
        internal object Pistol { get; }
        internal object InstalledMagazine { get; }
        internal object SpareMagazine { get; }
    }

    internal static class RuntimeArmamentService
    {
        const int MaximumStashItems = 4096;

        internal static bool TrySelect(object profile, int seed, string eligibleTemplateList, out RuntimeArmament armament)
        {
            armament = null;
            object inventory = ReadField(profile, "Inventory");
            object stash = ReadField(inventory, "Stash");
            MethodInfo visibleItems = stash?.GetType().GetMethod("GetAllVisibleItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (!(visibleItems?.Invoke(stash, null) is IEnumerable items)) return true;

            var all = new List<object>();
            foreach (object item in items)
            {
                if (all.Count == MaximumStashItems) return false;
                all.Add(item);
            }

            var allowedTemplates = new HashSet<string>(
                (eligibleTemplateList ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()),
                StringComparer.Ordinal);
            var pistols = new List<RuntimeArmament>();
            foreach (object pistol in all.Where(item => IsPistol(item) && IsDirectRoot(item, stash) && (allowedTemplates.Count == 0 || allowedTemplates.Contains(ReadString(item, "StringTemplateId")))))
            {
                object installed = pistol.GetType().GetMethod("GetCurrentMagazine", BindingFlags.Instance | BindingFlags.Public)?.Invoke(pistol, null);
                object magazineSlot = pistol.GetType().GetMethod("GetMagazineSlot", BindingFlags.Instance | BindingFlags.Public)?.Invoke(pistol, null);
                if (installed == null || magazineSlot == null) continue;
                foreach (object spare in all.Where(item => IsMagazine(item) && IsDirectRoot(item, stash) && !ReferenceEquals(item, installed)))
                {
                    if (ReadBooleanResult(magazineSlot, "CheckCompatibility", spare))
                        pistols.Add(new RuntimeArmament(pistol, installed, spare));
                }
            }

            if (pistols.Count == 0) return true;
            RuntimeArmament[] ordered = pistols
                .OrderBy(value => ReadString(value.Pistol, "Id"), StringComparer.Ordinal)
                .ThenBy(value => ReadString(value.SpareMagazine, "Id"), StringComparer.Ordinal)
                .ToArray();
            armament = ordered[PositiveModulo(seed, ordered.Length)];
            return true;
        }

        internal static async Task TransferAsync(object newPlayer, object recoveryEquipment, RuntimeArmament armament)
        {
            if (armament == null) return;
            object controller = ReadProperty(newPlayer, "InventoryController");
            object pistolOrigin = ReadProperty(armament.Pistol, "CurrentAddress");
            object spareOrigin = ReadProperty(armament.SpareMagazine, "CurrentAddress");
            if (controller == null || pistolOrigin == null || spareOrigin == null)
                throw new InvalidOperationException("selected stash armament lost its original address");

            bool pistolMoved = false;
            try
            {
                EnsureQuickMove(controller, recoveryEquipment, armament.Pistol, simulate: true);
                EnsureQuickMove(controller, recoveryEquipment, armament.SpareMagazine, simulate: true);
                await QuickMove(controller, recoveryEquipment, armament.Pistol);
                pistolMoved = true;
                if (!ReferenceEquals(
                        armament.Pistol.GetType().GetMethod("GetCurrentMagazine")?.Invoke(armament.Pistol, null),
                        armament.InstalledMagazine))
                    throw new InvalidOperationException("installed magazine identity changed during pistol transfer");
                await QuickMove(controller, recoveryEquipment, armament.SpareMagazine);
            }
            catch
            {
                if (pistolMoved) await MoveTo(controller, armament.Pistol, pistolOrigin);
                if (!ReferenceEquals(ReadProperty(armament.SpareMagazine, "CurrentAddress"), spareOrigin))
                    await MoveTo(controller, armament.SpareMagazine, spareOrigin);
                throw;
            }
        }

        static async Task QuickMove(object controller, object equipment, object item)
        {
            object operation = EnsureQuickMove(controller, equipment, item, simulate: false);
            await Run(controller, operation);
        }

        static object EnsureQuickMove(object controller, object equipment, object item, bool simulate)
        {
            Type manipulator = FindType("EFT.InventoryLogic.ItemManipulator");
            MethodInfo method = manipulator.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Single(value => value.Name == "QuickFindAppropriatePlace" && value.GetParameters().Length == 5);
            Type compound = FindType("EFT.InventoryLogic.CompoundItem");
            Array targets = Array.CreateInstance(compound, 1);
            targets.SetValue(equipment, 0);
            object order = Enum.ToObject(method.GetParameters()[3].ParameterType, 40);
            object operation = method.Invoke(null, new[] { item, controller, targets, order, (object)simulate });
            if (!ReadBoolean(operation, "Succeeded")) throw new InvalidOperationException("no valid recovery equipment slot for " + ReadString(item, "Id"));
            return operation;
        }

        static async Task MoveTo(object controller, object item, object address)
        {
            Type manipulator = FindType("EFT.InventoryLogic.ItemManipulator");
            MethodInfo move = manipulator.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Single(value => value.Name == "Move" && value.GetParameters().Length == 4);
            object operation = move.Invoke(null, new[] { item, address, controller, (object)false });
            if (!ReadBoolean(operation, "Succeeded")) throw new InvalidOperationException("inventory rollback operation was rejected");
            await Run(controller, operation);
        }

        static async Task Run(object controller, object operation)
        {
            MethodInfo run = FindMethod(controller.GetType(), "TryRunNetworkTransaction", 2);
            var task = run?.Invoke(controller, new[] { operation, null }) as Task;
            if (task == null) throw new InvalidOperationException("inventory controller rejected transaction startup");
            await task;
            object result = task.GetType().GetProperty("Result")?.GetValue(task, null);
            if (result == null || !ReadBoolean(result, "Succeed"))
                throw new InvalidOperationException("native inventory transaction failed");
        }

        static bool IsPistol(object item)
        {
            if (!IsType(item, "EFT.InventoryLogic.Weapon")) return false;
            object template = ReadProperty(item, "Template");
            return string.Equals(ReadField(template, "weapClass")?.ToString(), "pistol", StringComparison.OrdinalIgnoreCase);
        }
        static bool IsMagazine(object item) => IsType(item, "EFT.InventoryLogic.Magazine");
        static bool IsDirectRoot(object item, object stash)
        {
            object address = ReadProperty(item, "CurrentAddress");
            object container = ReadField(address, "Container");
            return ReferenceEquals(ReadProperty(container, "ParentItem"), stash);
        }
        static bool IsType(object value, string fullName) { Type target = FindType(fullName); return target != null && target.IsInstanceOfType(value); }
        static int PositiveModulo(int value, int length) => (int)((uint)value % (uint)length);
        static bool ReadBooleanResult(object instance, string method, object argument) => instance.GetType().GetMethod(method)?.Invoke(instance, new[] { argument }) is bool value && value;
        static bool ReadBoolean(object instance, string property) => ReadProperty(instance, property) is bool value && value;
        static string ReadString(object instance, string property) => ReadProperty(instance, property)?.ToString();
        static object ReadProperty(object instance, string name) => instance?.GetType().GetProperty(name)?.GetValue(instance, null);
        static object ReadField(object instance, string name) => instance == null ? null : FindField(instance.GetType(), name)?.GetValue(instance);
        static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).FirstOrDefault(type => type != null);
        static FieldInfo FindField(Type type, string name) { while (type != null) { FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (field != null) return field; type = type.BaseType; } return null; }
        static MethodInfo FindMethod(Type type, string name, int count) { while (type != null) { MethodInfo method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).SingleOrDefault(value => value.Name == name && value.GetParameters().Length == count); if (method != null) return method; type = type.BaseType; } return null; }
    }
}
