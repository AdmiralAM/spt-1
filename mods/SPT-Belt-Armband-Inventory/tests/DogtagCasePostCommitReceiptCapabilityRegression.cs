using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Models.Common;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCasePostCommitReceiptCapabilityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        var bear = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
        var usec = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
        var caseTpl = new MongoId(DogtagCaseItem.TemplateId);
        var foreign = new MongoId("5448bf274bdc2dfc2f8b456a");
        var vanilla = new HashSet<MongoId> { bear, usec };
        DogtagCaseHostContract.CaptureVanillaEntries(vanilla);

        // Successful external proof: the same pre-add token can be consumed as
        // metadata only, preserving the exact committed host shape.
        var accepted = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> acceptedReceipt = DogtagCaseHostContract.CaptureRollbackBaseline(accepted);
        accepted.Add(caseTpl);
        if (!DogtagCaseHostContract.TryAbandonRollbackAuthority(accepted, acceptedReceipt))
            throw new InvalidOperationException("Dogtag post-commit receipt capability regression failed: successful external proof must be able to consume exact host authority without removing the Case.");
        if (!accepted.Contains(caseTpl) || accepted.Count != vanilla.Count + 1)
            throw new InvalidOperationException("Dogtag post-commit receipt capability regression failed: accepted receipt must preserve baseline plus exact Case.");

        // Failed external proof: before acceptance, the same kind of exact token
        // can instead undo only this transaction's Case addition.
        var rejected = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> rejectedReceipt = DogtagCaseHostContract.CaptureRollbackBaseline(rejected);
        rejected.Add(caseTpl);
        if (!DogtagCaseHostContract.TryRollbackOwnedCaseAddition(rejected, rejectedReceipt)
            || !rejected.SetEquals(vanilla))
            throw new InvalidOperationException("Dogtag post-commit receipt capability regression failed: failed external proof must be able to roll back the exact owned host add to the pinned baseline.");

        // Foreign drift between host commit and external proof must remain fail-closed:
        // neither acceptance nor rollback may rewrite an ambiguous live host.
        var drifted = new HashSet<MongoId>(vanilla);
        HashSet<MongoId> driftReceipt = DogtagCaseHostContract.CaptureRollbackBaseline(drifted);
        drifted.Add(caseTpl);
        drifted.Add(foreign);
        if (DogtagCaseHostContract.TryAbandonRollbackAuthority(drifted, driftReceipt))
            throw new InvalidOperationException("Dogtag post-commit receipt capability regression failed: foreign drift must not be accepted as a clean post-commit host.");
        if (DogtagCaseHostContract.TryRollbackOwnedCaseAddition(drifted, driftReceipt))
            throw new InvalidOperationException("Dogtag post-commit receipt capability regression failed: foreign drift must not be rewritten by post-commit rollback.");
        if (!drifted.Contains(caseTpl) || !drifted.Contains(foreign))
            throw new InvalidOperationException("Dogtag post-commit receipt capability regression failed: ambiguous host state must remain untouched.");
    }
}
