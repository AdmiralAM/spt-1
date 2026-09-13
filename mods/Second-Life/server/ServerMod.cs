using System.Collections.Concurrent;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
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
        if (!reservations.TryRemove(key, out Reservation? reservation)) return new(false, false, 0, "reservation missing");
        foreach (Debit debit in reservation.Debits.Where(value => (value.Item.Upd?.StackObjectsCount ?? 0) <= 0))
            reservation.Items.Remove(debit.Item);
        return new(true, true, reservation.Cost, "committed");
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
    sealed record Reservation(int Cost, List<Item> Items, List<Debit> Debits);
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

[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public sealed class LoadNotice(ISptLogger<LoadNotice> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Success("Second Life Admiral server payment authority loaded");
        return Task.CompletedTask;
    }
}
