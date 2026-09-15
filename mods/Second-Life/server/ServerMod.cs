using System.Collections.Concurrent;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace Admiral.SecondLife.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.secondlife.server";
    public string Name { get; init; } = "Second Life Admiral Server";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("0.1.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/AdmiralAM/spt-1";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; }
}

public sealed record PaymentRequest(string Action, string Token, int Cost) : IRequestData;
public sealed record PaymentResponse(bool Ok, bool Reserved, int Available, string Message);
public sealed record ArmamentRequest(string Action, string Token, string EligibleTemplates) : IRequestData;
public sealed record ArmamentNode(string Id, string Template, string? ParentId, string? SlotId, string? LocationJson, string? UpdJson);
public sealed record ArmamentResponse(bool Ok, bool Reserved, string Message, List<ArmamentNode>? Items);

[Injectable]
public sealed class PaymentReservationService(ProfileHelper profiles)
{
    const string RubleTemplate = "5449016a4bdc2d6f028b456f";
    readonly ConcurrentDictionary<string, Reservation> reservations = new(StringComparer.Ordinal);
    readonly object sync = new();

    public PaymentResponse Handle(MongoId sessionId, PaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 128)
            return new(false, false, 0, "invalid token");
        string key = sessionId + ":" + request.Token;
        lock (sync)
        {
            return request.Action switch
            {
                "reserve" => Reserve(sessionId, key, request.Cost),
                "commit" => Commit(key),
                "finalize" => Finalize(key),
                "release" => Release(key),
                "refund" => Refund(key),
                _ => new(false, false, 0, "invalid action")
            };
        }
    }

    PaymentResponse Reserve(MongoId sessionId, string key, int cost)
    {
        if (cost < 0 || cost > 1_000_000) return new(false, false, 0, "invalid cost");
        if (reservations.TryGetValue(key, out Reservation? prior)) return new(true, true, prior.Cost, "already reserved");
        var profile = profiles.GetPmcProfile(sessionId);
        var inventory = profile?.Inventory;
        if (inventory?.Stash is null) return new(false, false, 0, "authoritative stash unavailable");
        string stashId = inventory.Stash.Value.ToString();
        List<Item> items = inventory.Items ?? [];
        Dictionary<string, Item> byId = items.ToDictionary(item => item.Id.ToString(), StringComparer.Ordinal);
        Item[] rubles = items
            .Where(item => item.Template.ToString() == RubleTemplate && IsBelow(item, stashId, byId))
            .OrderBy(item => item.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        int available = (int)Math.Min(int.MaxValue, rubles.Sum(item => Math.Max(0d, item.Upd?.StackObjectsCount ?? 1d)));
        if (available < cost) return new(true, false, available, "insufficient rubles");
        int remaining = cost;
        var debits = new List<Debit>();
        foreach (Item item in rubles)
        {
            if (remaining == 0) break;
            item.Upd ??= new Upd();
            int original = (int)(item.Upd.StackObjectsCount ?? 1d);
            int amount = Math.Min(original, remaining);
            item.Upd.StackObjectsCount = original - amount;
            debits.Add(new(item, original));
            remaining -= amount;
        }
        reservations[key] = new(cost, items, debits);
        return new(true, true, available, "reserved");
    }

    PaymentResponse Commit(string key)
    {
        if (!reservations.TryGetValue(key, out Reservation? reservation)) return new(false, false, 0, "reservation missing");
        reservation.Committed = true;
        return new(true, true, reservation.Cost, "committed");
    }

    PaymentResponse Finalize(string key)
    {
        if (!reservations.TryGetValue(key, out Reservation? reservation) || !reservation.Committed)
            return new(false, false, 0, "committed reservation missing");
        reservation.Finalized = true;
        return new(true, true, reservation.Cost, "finalized");
    }

    PaymentResponse Release(string key)
    {
        if (!reservations.TryGetValue(key, out Reservation? reservation) || !reservation.Finalized || !reservations.TryRemove(key, out _))
            return new(false, false, 0, "finalized reservation missing");
        foreach (Debit debit in reservation.Debits.Where(value => (value.Item.Upd?.StackObjectsCount ?? 0) <= 0))
            reservation.Items.Remove(debit.Item);
        return new(true, true, reservation.Cost, "released");
    }

    PaymentResponse Refund(string key)
    {
        if (!reservations.TryRemove(key, out Reservation? reservation)) return new(true, false, 0, "already released");
        foreach (Debit debit in reservation.Debits)
        {
            debit.Item.Upd ??= new Upd();
            debit.Item.Upd.StackObjectsCount = debit.Original;
        }
        return new(true, false, reservation.Cost, "refunded");
    }

    static bool IsBelow(Item item, string rootId, IReadOnlyDictionary<string, Item> byId)
    {
        string? parent = item.ParentId;
        for (int depth = 0; depth < 32 && !string.IsNullOrWhiteSpace(parent); depth++)
        {
            if (parent == rootId) return true;
            if (!byId.TryGetValue(parent, out Item? owner)) return false;
            parent = owner.ParentId;
        }
        return false;
    }

    sealed record Debit(Item Item, int Original);
    sealed class Reservation(int cost, List<Item> items, List<Debit> debits)
    {
        public int Cost { get; } = cost;
        public List<Item> Items { get; } = items;
        public List<Debit> Debits { get; } = debits;
        public bool Committed { get; set; }
        public bool Finalized { get; set; }
    }
}

[Injectable]
public sealed class ArmamentReservationService(ProfileHelper profiles, ItemHelper itemHelper, JsonUtil json)
{
    static readonly MongoId PistolBaseClass = new("5447b5cf4bdc2d65278b4567");
    readonly ConcurrentDictionary<string, Reservation> reservations = new(StringComparer.Ordinal);
    readonly object sync = new();

    public ArmamentResponse Handle(MongoId sessionId, ArmamentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 128)
            return new(false, false, "invalid token", null);
        string key = sessionId + ":" + request.Token;
        lock (sync)
        {
            return request.Action switch
            {
                "reserve" => Reserve(sessionId, key, request.EligibleTemplates),
                "commit" => Commit(key),
                "finalize" => Finalize(key),
                "release" => Release(key),
                "refund" => Refund(key),
                _ => new(false, false, "invalid action", null)
            };
        }
    }

    ArmamentResponse Reserve(MongoId sessionId, string key, string eligibleTemplates)
    {
        if (reservations.TryGetValue(key, out Reservation? prior))
            return new(true, true, "already reserved", prior.Nodes);
        var inventory = profiles.GetPmcProfile(sessionId)?.Inventory;
        if (inventory?.Stash is null) return new(false, false, "authoritative stash unavailable", null);
        List<Item> items = inventory.Items ?? [];
        string stashId = inventory.Stash.Value.ToString();
        Dictionary<string, Item> byId = items.ToDictionary(item => item.Id.ToString(), StringComparer.Ordinal);
        HashSet<string> allowed = eligibleTemplates.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        Item[] pistols = items.Where(item => IsBelow(item, stashId, byId) && itemHelper.IsOfBaseclass(item.Template, PistolBaseClass) && (allowed.Count == 0 || allowed.Contains(item.Template.ToString())))
            .OrderBy(item => item.Id.ToString(), StringComparer.Ordinal).ToArray();
        var candidates = new List<(Item Pistol, Item Spare)>();
        foreach (Item pistol in pistols)
        {
            Item? installed = items.FirstOrDefault(item => item.ParentId == pistol.Id.ToString() && string.Equals(item.SlotId, "mod_magazine", StringComparison.Ordinal));
            if (installed is null) continue;
            Item? spare = items.Where(item => item.Id != installed.Id && item.Template == installed.Template && IsBelow(item, stashId, byId) && !IsBelow(item, pistol.Id.ToString(), byId))
                .OrderBy(item => item.Id.ToString(), StringComparer.Ordinal).FirstOrDefault();
            if (spare is not null) candidates.Add((pistol, spare));
        }
        if (candidates.Count == 0) return new(true, false, "no complete owned pistol set", null);
        int index = (int)((uint)StringComparer.Ordinal.GetHashCode(key) % (uint)candidates.Count);
        (Item selectedPistol, Item selectedSpare) = candidates[index];
        HashSet<string> roots = [selectedPistol.Id.ToString(), selectedSpare.Id.ToString()];
        List<Item> selected = items.Where(item => roots.Contains(item.Id.ToString()) || roots.Any(root => IsBelow(item, root, byId))).ToList();
        List<ArmamentNode> nodes = selected.Select(item => new ArmamentNode(
            item.Id.ToString(), item.Template.ToString(), roots.Contains(item.Id.ToString()) ? null : item.ParentId,
            roots.Contains(item.Id.ToString()) ? null : item.SlotId,
            item.Location is null ? null : json.Serialize(item.Location), item.Upd is null ? null : json.Serialize(item.Upd))).ToList();
        foreach (Item item in selected) items.Remove(item);
        reservations[key] = new(items, selected, nodes);
        return new(true, true, "reserved", nodes);
    }

    ArmamentResponse Commit(string key)
    {
        if (!reservations.TryGetValue(key, out Reservation? reservation)) return new(false, false, "reservation missing", null);
        reservation.Committed = true;
        return new(true, true, "committed", null);
    }

    ArmamentResponse Finalize(string key)
    {
        if (!reservations.TryGetValue(key, out Reservation? reservation) || !reservation.Committed)
            return new(false, false, "committed reservation missing", null);
        reservation.Finalized = true;
        return new(true, true, "finalized", null);
    }

    ArmamentResponse Release(string key) =>
        reservations.TryGetValue(key, out Reservation? reservation) && reservation.Finalized && reservations.TryRemove(key, out _)
            ? new(true, true, "released", null)
            : new(false, false, "finalized reservation missing", null);

    ArmamentResponse Refund(string key)
    {
        if (!reservations.TryRemove(key, out Reservation? reservation)) return new(true, false, "already released", null);
        reservation.Inventory.AddRange(reservation.Items);
        return new(true, false, "refunded", null);
    }

    static bool IsBelow(Item item, string rootId, IReadOnlyDictionary<string, Item> byId)
    {
        string? parent = item.ParentId;
        for (int depth = 0; depth < 32 && !string.IsNullOrWhiteSpace(parent); depth++)
        {
            if (parent == rootId) return true;
            if (!byId.TryGetValue(parent, out Item? owner)) return false;
            parent = owner.ParentId;
        }
        return false;
    }

    sealed class Reservation(List<Item> inventory, List<Item> items, List<ArmamentNode> nodes)
    {
        public List<Item> Inventory { get; } = inventory;
        public List<Item> Items { get; } = items;
        public List<ArmamentNode> Nodes { get; } = nodes;
        public bool Committed { get; set; }
        public bool Finalized { get; set; }
    }
}

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public sealed class PaymentRouter(JsonUtil json, PaymentReservationService service)
    : StaticRouter(json,
        [new RouteAction<PaymentRequest>("/second-life/v1/payment", (url, request, session, output, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(json.Serialize(service.Handle(session, request))!);
        })])
{ }

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public sealed class ArmamentRouter(JsonUtil json, ArmamentReservationService service)
    : StaticRouter(json,
        [new RouteAction<ArmamentRequest>("/second-life/v1/armament", (url, request, session, output, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(json.Serialize(service.Handle(session, request))!);
        })])
{ }

[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public sealed class LoadNotice(ISptLogger<LoadNotice> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Success("Second Life Admiral server payment authority loaded");
        return Task.CompletedTask;
    }
}
