using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SPTItemIntelligence
{
    public sealed class RaidInventoryRuntimeScanner
    {
        readonly Action<string> logInfo;
        int confirmed;
        public RaidInventoryRuntimeScanner(Action<string> logInfo = null) { this.logInfo = logInfo; }

        public bool CaptureBaseline(RaidRequirementLedger ledger)
        {
            List<RaidInventoryItemSnapshot> items;
            return ledger != null && TryRead(out items) && ledger.CaptureInitialInventory(items);
        }

        public bool Refresh(RaidRequirementLedger ledger)
        {
            List<RaidInventoryItemSnapshot> items;
            if (ledger == null || !TryRead(out items)) return false;
            bool changed = ledger.ReplaceFromPlayerInventory(items);
            if (System.Threading.Interlocked.Exchange(ref confirmed, 1) == 0 && logInfo != null)
                logInfo("Item Intelligence player inventory scanner active; records=" + items.Count + ".");
            return changed;
        }

        static bool TryRead(out List<RaidInventoryItemSnapshot> result)
        {
            result = new List<RaidInventoryItemSnapshot>();
            Type gameWorldType = FindType("EFT.GameWorld");
            Type singletonOpen = FindType("Comfort.Common.Singleton`1");
            if (gameWorldType == null || singletonOpen == null) return false;
            Type singleton = singletonOpen.MakeGenericType(gameWorldType);
            object instantiated = Member(singleton, "Instantiated");
            if (instantiated is bool && !(bool)instantiated) return false;
            object world = Member(singleton, "Instance");
            object player = Member(world, "MainPlayer");
            object inventory = Member(player, "Inventory");
            IEnumerable items = Member(inventory, "AllRealPlayerItems") as IEnumerable;
            if (items == null) return false;
            foreach (object item in items)
            {
                string id = Text(Member(item, "Id", "ID"));
                string template = Text(Member(item, "TemplateId", "Tpl"));
                if (id.Length == 0 || template.Length == 0) continue;
                int stack = Math.Max(1, Number(Member(item, "StackObjectsCount"), 1));
                bool fir = Flag(Member(item, "SpawnedInSession"));
                result.Add(new RaidInventoryItemSnapshot(id, template, stack, fir));
            }
            return true;
        }

        static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        static object Member(object source, params string[] names)
        {
            if (source == null) return null;
            Type original = source as Type ?? source.GetType();
            object target = source is Type ? null : source;
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (target == null ? BindingFlags.Static : BindingFlags.Instance);
            for (int i = 0; i < names.Length; i++)
                for (Type type = original; type != null; type = type.BaseType)
                {
                    PropertyInfo property = type.GetProperty(names[i], flags | BindingFlags.DeclaredOnly);
                    if (property != null) { try { return property.GetValue(target, null); } catch { } }
                    FieldInfo field = type.GetField(names[i], flags | BindingFlags.DeclaredOnly);
                    if (field != null) { try { return field.GetValue(target); } catch { } }
                }
            return null;
        }

        static string Text(object value) { return value == null ? string.Empty : value.ToString().Trim(); }
        static bool Flag(object value) { try { return value != null && Convert.ToBoolean(value); } catch { return false; } }
        static int Number(object value, int fallback) { try { return value == null ? fallback : Convert.ToInt32(value); } catch { return fallback; } }
    }
}
