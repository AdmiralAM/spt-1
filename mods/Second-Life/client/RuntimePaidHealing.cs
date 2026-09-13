using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Admiral.SecondLife.Client
{
    internal sealed class RuntimePaidHealing
    {
        const string TherapistId = "54cb57776803fa99248b456e";
        const string RubleTemplateId = "5449016a4bdc2d6f028b456f";
        readonly List<MoneyDebit> debits;
        bool applied;

        RuntimePaidHealing(int cost, List<MoneyDebit> debits) { Cost = cost; this.debits = debits; }
        internal int Cost { get; }

        internal static bool TryPrepare(object profile, object player, out RuntimePaidHealing healing, out string failure)
        {
            healing = null;
            failure = null;
            try
            {
                float missingHealth = ReadMissingHealth(player);
                if (missingHealth <= 0f) return Fail("native health state has no recoverable damage", out failure);
                object traderInfo = FindTherapistInfo(profile);
                if (traderInfo == null) return Fail("Therapist loyalty data is unavailable", out failure);
                int cost = CalculateNativeCost(profile, traderInfo, missingHealth);
                if (!TrySelectRubles(profile, cost, out List<MoneyDebit> debits))
                    return Fail("not enough rubles in the stash for native healing price " + cost, out failure);
                healing = new RuntimePaidHealing(cost, debits);
                return true;
            }
            catch (Exception exception) { return Fail("native healing contract rejected: " + Unwrap(exception).Message, out failure); }
        }

        internal void Apply(object recoveredPlayer)
        {
            if (applied) throw new InvalidOperationException("paid healing was already applied");
            foreach (MoneyDebit debit in debits) WriteField(debit.Item, "StackObjectsCount", debit.OriginalCount - debit.Amount);
            try
            {
                object controller = ReadProperty(recoveredPlayer, "ActiveHealthController") ?? ReadProperty(recoveredPlayer, "HealthController");
                MethodInfo restore = controller?.GetType().GetMethod("RestoreFullHealth", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (restore == null) throw new InvalidOperationException("recovered player has no native RestoreFullHealth operation");
                restore.Invoke(controller, null);
                applied = true;
            }
            catch { Rollback(); throw; }
        }

        internal void Rollback()
        {
            foreach (MoneyDebit debit in debits) WriteField(debit.Item, "StackObjectsCount", debit.OriginalCount);
            applied = false;
        }

        internal void FinalizeDebit(object recoveredPlayer)
        {
            if (!applied) return;
            object controller = ReadProperty(recoveredPlayer, "InventoryController");
            Type manipulator = FindType("EFT.InventoryLogic.ItemManipulator");
            MethodInfo remove = manipulator?.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .SingleOrDefault(method => method.Name == "Remove" && method.GetParameters().Length == 3);
            if (controller == null || remove == null) return;
            foreach (MoneyDebit debit in debits.Where(value => value.OriginalCount == value.Amount))
            {
                try { remove.Invoke(null, new[] { debit.Item, controller, (object)false }); }
                catch { }
            }
        }

        internal static bool TryShowNativeConfirmation(int cost, Action accept, Action cancel, out string failure)
        {
            failure = null;
            try
            {
                Type contextType = FindType("EFT.UI.ItemUiContext");
                object context = contextType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null, null);
                MethodInfo show = contextType?.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .SingleOrDefault(method => method.Name == "ShowMessageWindow" && method.GetParameters().Length == 7);
                if (context == null || show == null) return Fail("native confirmation window is unavailable", out failure);
                show.Invoke(context, new object[]
                {
                    "Восстановить здоровье перед возвращением в рейд за " + cost + " ₽?\nОтказ завершит рейд обычной смертью.",
                    accept, cancel, "Second Life Admiral", 0f, true, Enum.ToObject(show.GetParameters()[6].ParameterType, 514)
                });
                return true;
            }
            catch (Exception exception) { return Fail("native confirmation window failed: " + Unwrap(exception).Message, out failure); }
        }

        static float ReadMissingHealth(object player)
        {
            object controller = ReadProperty(player, "HealthController");
            Type bodyPartType = FindType("EBodyPart");
            var bodyParts = FindType("EFT.HealthSystem.HealthHelper")?.GetField("RealBodyParts", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as IEnumerable;
            MethodInfo getHealth = controller?.GetType().GetMethod("GetBodyPartHealth", new[] { bodyPartType, typeof(bool) });
            if (controller == null || bodyParts == null || getHealth == null) throw new InvalidOperationException("active body-part health contract is unavailable");
            float missing = 0f;
            foreach (object bodyPart in bodyParts)
            {
                object value = getHealth.Invoke(controller, new[] { bodyPart, (object)false });
                missing += Convert.ToSingle(ReadField(value, "Maximum")) - Convert.ToSingle(ReadField(value, "Current"));
            }
            return Math.Max(0f, missing);
        }

        static int CalculateNativeCost(object profile, object traderInfo, float missingHealth)
        {
            Type globalType = FindType("EFT.GlobalConfiguration");
            Type singletonOpen = FindType("Comfort.Common.Singleton`1");
            object global = singletonOpen?.MakeGenericType(globalType).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null, null);
            object priceSettings = ReadField(ReadField(global, "Health"), "HealPrice");
            if (priceSettings == null) throw new InvalidOperationException("global native heal-price settings are unavailable");
            MethodInfo free = priceSettings.GetType().GetMethod("IsFastHealFree", BindingFlags.Instance | BindingFlags.Public);
            if (free?.Invoke(priceSettings, new[] { profile }) is bool isFree && isFree) return 0;
            float pointPrice = Convert.ToSingle(ReadField(priceSettings, "HealthPointPrice"));
            object loyalty = ReadProperty(traderInfo, "CurrentLoyalty");
            float loyaltyCoefficient = Clamp(Convert.ToSingle(ReadField(loyalty, "HealPriceCoef")) / 100f, 0.05f, 10f);
            object skills = ReadField(profile, "Skills");
            int charismaLevel = Math.Min(50, Convert.ToInt32(ReadProperty(ReadField(skills, "Charisma"), "Level")));
            float healingBuff = Convert.ToSingle(ReadField(ReadField(skills, "CharismaHealingDiscount"), "Value"));
            object levelBonus = ReadField(ReadField(ReadField(ReadProperty(skills, "Settings"), "Charisma"), "BonusSettings"), "LevelBonusSettings");
            float restoreDiscount = Convert.ToSingle(ReadField(levelBonus, "HealthRestoreTraderDiscount"));
            int loyaltyLevel = Convert.ToInt32(ReadProperty(traderInfo, "LoyaltyLevel"));
            return PaidHealingPolicy.CalculateCost(
                missingHealth, pointPrice, loyaltyCoefficient * 100f, false,
                healingBuff, charismaLevel, loyaltyLevel, restoreDiscount);
        }

        static object FindTherapistInfo(object profile)
        {
            var traders = ReadProperty(profile, "TradersInfo") as IDictionary;
            if (traders == null) return null;
            foreach (DictionaryEntry entry in traders) if (string.Equals(entry.Key?.ToString(), TherapistId, StringComparison.Ordinal)) return entry.Value;
            return null;
        }

        static bool TrySelectRubles(object profile, int cost, out List<MoneyDebit> debits)
        {
            debits = new List<MoneyDebit>();
            if (cost == 0) return true;
            object stash = ReadField(ReadField(profile, "Inventory"), "Stash");
            var items = stash?.GetType().GetMethod("GetAllVisibleItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(stash, null) as IEnumerable;
            if (items == null) return false;
            object[] rubles = items.Cast<object>().Where(item => string.Equals(ReadProperty(item, "StringTemplateId")?.ToString(), RubleTemplateId, StringComparison.Ordinal)).OrderBy(item => ReadProperty(item, "Id")?.ToString(), StringComparer.Ordinal).ToArray();
            int[] counts = rubles.Select(item => Convert.ToInt32(ReadField(item, "StackObjectsCount"))).ToArray();
            IReadOnlyList<int> plan = PaidHealingPolicy.PlanDebits(cost, counts);
            if (plan == null) return false;
            for (int index = 0; index < rubles.Length; index++)
            {
                if (plan[index] > 0) debits.Add(new MoneyDebit(rubles[index], counts[index], plan[index]));
            }
            return true;
        }

        static float Clamp(float value, float minimum, float maximum) => Math.Max(minimum, Math.Min(maximum, value));
        static object ReadProperty(object instance, string name) => instance?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance, null);
        static object ReadField(object instance, string name) => instance == null ? null : FindField(instance.GetType(), name)?.GetValue(instance);
        static void WriteField(object instance, string name, object value) => FindField(instance.GetType(), name).SetValue(instance, value);
        static FieldInfo FindField(Type type, string name) { while (type != null) { FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (field != null) return field; type = type.BaseType; } return null; }
        static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).FirstOrDefault(type => type != null);
        static Exception Unwrap(Exception exception) => exception is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : exception;
        static bool Fail(string message, out string failure) { failure = message; return false; }

        sealed class MoneyDebit
        {
            internal MoneyDebit(object item, int originalCount, int amount) { Item = item; OriginalCount = originalCount; Amount = amount; }
            internal object Item { get; }
            internal int OriginalCount { get; }
            internal int Amount { get; }
        }
    }
}
