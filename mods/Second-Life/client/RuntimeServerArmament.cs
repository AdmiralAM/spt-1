using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Admiral.SecondLife.Client
{
    internal sealed class RuntimeServerArmamentReservation
    {
        const string Route = "/second-life/v1/armament";
        readonly string token;
        bool active;
        bool committed;

        RuntimeServerArmamentReservation(string token, RuntimeArmament armament, bool active)
        {
            this.token = token;
            Armament = armament;
            this.active = active;
        }

        internal RuntimeArmament Armament { get; }

        internal static bool TryReserve(string eligibleTemplates, out RuntimeServerArmamentReservation reservation, out string failure)
        {
            reservation = null;
            failure = null;
            string token = Guid.NewGuid().ToString("N");
            if (!Send("reserve", token, eligibleTemplates ?? string.Empty, out ArmamentResponse response, out failure)) return false;
            if (!response.Reserved || response.Items == null || response.Items.Count == 0)
            {
                reservation = new RuntimeServerArmamentReservation(token, null, false);
                return true;
            }
            try
            {
                RuntimeArmament armament = Build(response.Items);
                reservation = new RuntimeServerArmamentReservation(token, armament, true);
                return true;
            }
            catch (Exception exception)
            {
                Send("refund", token, string.Empty, out _, out _);
                failure = "reserved stash armament could not be reconstructed: " + (exception.InnerException?.Message ?? exception.Message);
                return false;
            }
        }

        internal void Commit()
        {
            if (!active) return;
            if (!Send("commit", token, string.Empty, out ArmamentResponse response, out string failure) || !response.Reserved)
                throw new InvalidOperationException("armament commit failed: " + failure);
            committed = true;
        }

        internal void FinalizeReservation()
        {
            if (!active) return;
            string failure = null;
            if (!committed || !Send("finalize", token, string.Empty, out ArmamentResponse response, out failure) || !response.Reserved)
                throw new InvalidOperationException("armament finalization failed: " + failure);
        }

        internal void ReleaseReservation()
        {
            if (!active) return;
            Send("release", token, string.Empty, out _, out _);
            active = false;
        }

        internal void Refund()
        {
            if (!active) return;
            if (!Send("refund", token, string.Empty, out _, out string failure))
                throw new InvalidOperationException("armament refund failed: " + failure);
            active = false;
        }

        static RuntimeArmament Build(List<ArmamentNode> nodes)
        {
            Type flatType = FindType("JsonType.FlatItem") ?? throw new InvalidOperationException("FlatItem type is unavailable");
            Type mongoType = FindType("EFT.MongoID") ?? throw new InvalidOperationException("MongoID type is unavailable");
            Type unparsedType = FindType("UnparsedData") ?? throw new InvalidOperationException("UnparsedData type is unavailable");
            ConstructorInfo mongoCtor = mongoType.GetConstructor(new[] { typeof(string) });
            Array flat = Array.CreateInstance(flatType, nodes.Count);
            for (int index = 0; index < nodes.Count; index++)
            {
                ArmamentNode node = nodes[index];
                object value = Activator.CreateInstance(flatType);
                flatType.GetField("_id").SetValue(value, mongoCtor.Invoke(new object[] { node.Id }));
                flatType.GetField("_tpl").SetValue(value, mongoCtor.Invoke(new object[] { node.Template }));
                if (!string.IsNullOrWhiteSpace(node.ParentId))
                {
                    object parent = mongoCtor.Invoke(new object[] { node.ParentId });
                    flatType.GetField("parentId").SetValue(value, Activator.CreateInstance(flatType.GetField("parentId").FieldType, parent));
                }
                flatType.GetField("slotId").SetValue(value, node.SlotId);
                if (!string.IsNullOrWhiteSpace(node.LocationJson)) flatType.GetField("location").SetValue(value, JsonConvert.DeserializeObject(node.LocationJson, unparsedType));
                if (!string.IsNullOrWhiteSpace(node.UpdJson)) flatType.GetField("upd").SetValue(value, JsonConvert.DeserializeObject(node.UpdJson, unparsedType));
                flat.SetValue(value, index);
            }

            Type factoryType = FindType("EFT.ItemFactory");
            Type singleton = FindType("Comfort.Common.Singleton`1")?.MakeGenericType(factoryType);
            object factory = singleton?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null, null);
            MethodInfo convert = factoryType?.GetMethod("FlatItemsToTree", BindingFlags.Instance | BindingFlags.Public);
            object result = convert?.Invoke(factory, new object[] { flat, true, null }) ?? throw new InvalidOperationException("FlatItemsToTree returned no result");
            var items = result.GetType().GetField("Items")?.GetValue(result) as IDictionary;
            if (items == null) throw new InvalidOperationException("deserialized armament dictionary is unavailable");
            object pistol = null;
            object spare = null;
            foreach (ArmamentNode root in nodes.Where(node => string.IsNullOrWhiteSpace(node.ParentId)))
            {
                object item = items[root.Id];
                if (IsType(item, "EFT.InventoryLogic.Weapon")) pistol = item;
                else if (IsType(item, "EFT.InventoryLogic.Magazine")) spare = item;
            }
            object installed = pistol?.GetType().GetMethod("GetCurrentMagazine", BindingFlags.Instance | BindingFlags.Public)?.Invoke(pistol, null);
            if (pistol == null || installed == null || spare == null) throw new InvalidOperationException("complete pistol and two-magazine tree was not restored");
            return new RuntimeArmament(pistol, installed, spare, detachedRoots: true);
        }

        static bool Send(string action, string token, string eligible, out ArmamentResponse response, out string failure)
        {
            response = null;
            failure = null;
            try
            {
                Type handler = FindType("SPT.Common.Http.RequestHandler");
                MethodInfo post = handler?.GetMethod("PostJson", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string) }, null);
                if (post == null) return Fail("SPT server transport is unavailable", out failure);
                string body = JsonConvert.SerializeObject(new { Action = action, Token = token, EligibleTemplates = eligible });
                string json = post.Invoke(null, new object[] { Route, body }) as string;
                response = JsonConvert.DeserializeObject<ArmamentResponse>(json ?? string.Empty);
                if (response == null || !response.Ok) return Fail(response?.Message ?? "SPT armament server rejected the request", out failure);
                return true;
            }
            catch (Exception exception) { return Fail("SPT armament request failed: " + (exception.InnerException?.Message ?? exception.Message), out failure); }
        }

        static bool IsType(object value, string name) { Type type = FindType(name); return value != null && type != null && type.IsInstanceOfType(value); }
        static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).FirstOrDefault(type => type != null);
        static bool Fail(string message, out string failure) { failure = message; return false; }

        sealed class ArmamentResponse
        {
            public bool Ok { get; set; }
            public bool Reserved { get; set; }
            public string Message { get; set; }
            public List<ArmamentNode> Items { get; set; }
        }

        sealed class ArmamentNode
        {
            public string Id { get; set; }
            public string Template { get; set; }
            public string ParentId { get; set; }
            public string SlotId { get; set; }
            public string LocationJson { get; set; }
            public string UpdJson { get; set; }
        }
    }
}
