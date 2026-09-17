using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SPTItemIntelligence
{
    public sealed class RaidInventoryRuntimeScanner
    {
        readonly Action<string> logInfo;
        static readonly Dictionary<Type, ItemAccessors> ItemAccessorsByType = new Dictionary<Type, ItemAccessors>();
        int confirmed;
        public RaidInventoryRuntimeScanner(Action<string> logInfo = null) { this.logInfo = logInfo; }

        public bool CaptureBaseline(RaidRequirementLedger ledger)
        {
            List<RaidInventoryItemSnapshot> items;
            bool raidActive;
            return ledger != null && TryRead(out items, out raidActive) && raidActive && ledger.CaptureInitialInventory(items);
        }

        public bool Refresh(RaidRequirementLedger ledger)
        {
            List<RaidInventoryItemSnapshot> items;
            bool raidActive;
            if (ledger == null) return false;
            if (!TryRead(out items, out raidActive))
            {
                if (!raidActive && ledger.IsRaidSessionActive)
                {
                    ledger.Reset();
                    return true;
                }
                return false;
            }
            bool changed = ledger.ReplaceFromPlayerInventory(items);
            if (System.Threading.Interlocked.Exchange(ref confirmed, 1) == 0 && logInfo != null)
                logInfo("Item Intelligence player inventory scanner active; records=" + items.Count + ".");
            return changed;
        }

        static bool TryRead(out List<RaidInventoryItemSnapshot> result, out bool raidActive)
        {
            result = new List<RaidInventoryItemSnapshot>();
            raidActive = false;
            Type gameWorldType = FindType("EFT.GameWorld");
            Type singletonOpen = FindType("Comfort.Common.Singleton`1");
            if (gameWorldType == null || singletonOpen == null) return false;
            Type singleton = singletonOpen.MakeGenericType(gameWorldType);
            object instantiated = Member(singleton, "Instantiated");
            if (instantiated is bool && !(bool)instantiated) return false;
            object world = Member(singleton, "Instance");
            object player = Member(world, "MainPlayer");
            // EFT keeps a GameWorld and a HideoutPlayer alive in the hideout.  Treating that
            // inventory as a raid made the card show "in raid" while looking at hideout upgrades.
            // The concrete player class is the reliable boundary and does not require a scene scan.
            raidActive = world != null && player != null && !IsHideoutPlayer(player);
            if (!raidActive) return false;
            object inventory = Member(player, "Inventory");
            IEnumerable items = Member(inventory, "AllRealPlayerItems") as IEnumerable;
            if (items == null) return false;
            foreach (object item in items)
            {
                if (item == null) continue;
                ItemAccessors accessors = GetAccessors(item.GetType());
                string id = Text(accessors.GetId(item));
                string template = Text(accessors.GetTemplate(item));
                if (id.Length == 0 || template.Length == 0) continue;
                int stack = Math.Max(1, Number(accessors.GetStack(item), 1));
                bool fir = Flag(accessors.GetFoundInRaid(item));
                result.Add(new RaidInventoryItemSnapshot(id, template, stack, fir));
            }
            return true;
        }

        internal static bool IsHideoutPlayerTypeName(string typeName)
        {
            return !string.IsNullOrWhiteSpace(typeName) &&
                typeName.IndexOf("HideoutPlayer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsHideoutPlayer(object player)
        {
            if (player == null) return false;
            for (Type type = player.GetType(); type != null; type = type.BaseType)
            {
                string name = type.FullName ?? type.Name;
                if (IsHideoutPlayerTypeName(name)) return true;
            }
            return Flag(Member(player, "IsHideout", "IsHideoutPlayer"));
        }

        static ItemAccessors GetAccessors(Type type)
        {
            ItemAccessors result;
            if (ItemAccessorsByType.TryGetValue(type, out result)) return result;
            result = new ItemAccessors(type);
            ItemAccessorsByType[type] = result;
            return result;
        }

        sealed class ItemAccessors
        {
            readonly MemberInfo id;
            readonly MemberInfo template;
            readonly MemberInfo stack;
            readonly MemberInfo foundInRaid;

            internal ItemAccessors(Type type)
            {
                id = Find(type, "Id", "ID");
                template = Find(type, "TemplateId", "Tpl");
                stack = Find(type, "StackObjectsCount");
                foundInRaid = Find(type, "SpawnedInSession");
            }

            internal object GetId(object item) { return Get(id, item); }
            internal object GetTemplate(object item) { return Get(template, item); }
            internal object GetStack(object item) { return Get(stack, item); }
            internal object GetFoundInRaid(object item) { return Get(foundInRaid, item); }

            static MemberInfo Find(Type type, params string[] names)
            {
                for (Type current = type; current != null; current = current.BaseType)
                    for (int i = 0; i < names.Length; i++)
                    {
                        PropertyInfo property = current.GetProperty(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (property != null && property.GetIndexParameters().Length == 0) return property;
                        FieldInfo field = current.GetField(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (field != null) return field;
                    }
                return null;
            }

            static object Get(MemberInfo member, object item)
            {
                try
                {
                    PropertyInfo property = member as PropertyInfo;
                    if (property != null) return property.GetValue(item, null);
                    FieldInfo field = member as FieldInfo;
                    return field == null ? null : field.GetValue(item);
                }
                catch { return null; }
            }
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
