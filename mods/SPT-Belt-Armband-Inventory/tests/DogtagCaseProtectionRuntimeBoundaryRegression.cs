using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;
using SPTBeltArmbandInventory.Server;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class DogtagCaseProtectionRuntimeBoundaryRegression
    {
        [ModuleInitializer]
        internal static void Run()
        {
            Assembly serverAssembly = typeof(DogtagCaseHostContract).Assembly;
            Type runtimeType = serverAssembly.GetType("SPTBeltArmbandInventory.Server.WearableProtectionRuntime", throwOnError: true);
            PropertyInfo activeRootsProperty = runtimeType.GetProperty("ActiveRoots", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(activeRootsProperty != null,
                "server WearableProtectionRuntime.ActiveRoots must remain an explicit non-public publication boundary");

            object published = activeRootsProperty.GetValue(null);
            Assert(published is IEnumerable,
                "server protection runtime must publish an enumerable detached root snapshot");

            int count = 0;
            foreach (object root in (IEnumerable)published)
            {
                Assert(root != null, "published protection root must not be null");
                Type rootType = root.GetType();
                PropertyInfo slotProperty = rootType.GetProperty("SlotId", BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo templateProperty = rootType.GetProperty("TemplateId", BindingFlags.Instance | BindingFlags.Public);
                Assert(slotProperty != null && templateProperty != null,
                    "published protection roots must preserve exact slot/template identity fields");

                string slotId = slotProperty.GetValue(root) as string;
                string templateId = templateProperty.GetValue(root) as string;
                Assert(!string.Equals(slotId, "Dogtag", StringComparison.Ordinal),
                    "stock EquipmentSlot.Dogtag must never become a B&A&HB protected death/insurance root");
                Assert(!string.Equals(templateId, RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal),
                    "Dogtag Case must never enter B&A&HB protected death/insurance root publication");
                count++;
            }

            Assert(count == 4,
                "default protection runtime must contain only the two ArmBand products, Magazine Belt and Emergency HeadBand roots");
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Dogtag Case protection runtime boundary regression failed: " + message);
        }
    }
}