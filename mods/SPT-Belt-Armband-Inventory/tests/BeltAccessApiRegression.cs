using System;

namespace SPTBeltArmbandInventory.Tests;

internal static class BeltAccessApiRegression
{
    internal static void Run()
    {
        BeltAccessApi.ResetForRegression();
        Require(BeltAccessApi.ContractVersion == 1, "public contract version must remain explicit");
        Require(!BeltAccessApi.IsAvailable, "API must start unavailable before exact runtime publication");
        Require(!BeltAccessApi.TryEnumerateBeltSources(new object(), out object[] unavailable) && unavailable.Length == 0,
            "unpublished API must fail closed with an empty result");

        object owner = new();
        object first = new();
        object second = new();
        Func<object, object[]> provider = _ => [first, second];
        Require(BeltAccessApi.TryPublish(owner, provider), "one exact owner must publish the API");
        Require(BeltAccessApi.TryPublish(owner, provider), "same owner/provider publication must be idempotent");
        Require(!BeltAccessApi.TryPublish(owner, _ => [first]), "same owner must not replace its provider while published");
        Require(!BeltAccessApi.TryPublish(new object(), provider), "a second adapter owner must be rejected");
        Require(BeltAccessApi.IsAvailable, "successful publication must advertise availability");
        Require(BeltAccessApi.TryEnumerateBeltSources(new object(), out object[] sources), "healthy provider must enumerate");
        Require(sources.Length == 2 && ReferenceEquals(sources[0], first) && ReferenceEquals(sources[1], second),
            "API must preserve source identity and order");
        sources[0] = null;
        Require(BeltAccessApi.TryEnumerateBeltSources(new object(), out object[] secondSnapshot) && ReferenceEquals(secondSnapshot[0], first),
            "consumer mutation must not alter the provider snapshot");

        BeltAccessApi.Revoke(new object());
        Require(BeltAccessApi.IsAvailable, "foreign revoke must not remove the owner");
        BeltAccessApi.Revoke(owner);
        Require(!BeltAccessApi.IsAvailable, "owner revoke must close the API");

        object throwingOwner = new();
        Require(BeltAccessApi.TryPublish(throwingOwner, _ => throw new InvalidOperationException("boom")), "throwing provider may publish before invocation");
        Require(!BeltAccessApi.TryEnumerateBeltSources(new object(), out object[] failed) && failed.Length == 0,
            "provider failure must remain contained and fail closed");
        BeltAccessApi.Revoke(throwingOwner);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Belt access API regression failed: " + message + ".");
    }
}
