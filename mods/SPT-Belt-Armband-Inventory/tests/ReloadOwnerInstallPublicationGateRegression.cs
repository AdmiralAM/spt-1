using System;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class ReloadOwnerInstallPublicationGateRegression
    {
        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Initialize()
        {
            Run();
        }

        internal static void Run()
        {
            ReloadOwnerInstallPublicationGate.ResetForRegression();
            if (!ReloadOwnerInstallPublicationGate.CanPublishForRegression())
                throw new InvalidOperationException("reload owner publication gate must start open outside an install transaction");

            object vanilla = new object();
            object candidate = new object();
            if (!ReferenceEquals(ReloadOwnerInstallPublicationGate.SelectCandidateForRegression(vanilla, candidate), candidate))
                throw new InvalidOperationException("open publication gate must preserve the candidate result");

            ReloadOwnerInstallPublicationGate.BeginForRegression();
            if (ReloadOwnerInstallPublicationGate.CanPublishForRegression())
                throw new InvalidOperationException("partial FastAccess install must close reload publication");
            if (!ReferenceEquals(ReloadOwnerInstallPublicationGate.SelectCandidateForRegression(vanilla, candidate), vanilla))
                throw new InvalidOperationException("closed publication gate must return the exact incoming vanilla result reference");

            ReloadOwnerInstallPublicationGate.BeginForRegression();
            ReloadOwnerInstallPublicationGate.EndForRegression();
            if (ReloadOwnerInstallPublicationGate.CanPublishForRegression())
                throw new InvalidOperationException("nested install completion must not reopen publication while the outer install is active");

            ReloadOwnerInstallPublicationGate.EndForRegression();
            if (!ReloadOwnerInstallPublicationGate.CanPublishForRegression())
                throw new InvalidOperationException("publication may reopen only after all install transactions have completed");

            ReloadOwnerInstallPublicationGate.EndForRegression();
            if (!ReloadOwnerInstallPublicationGate.CanPublishForRegression())
                throw new InvalidOperationException("extra finalization must fail closed against depth underflow and leave the idle transaction gate open");

            if (!ReloadOwnerInstallPublicationGate.HasPublicationAuthorityForRegression(true, false, 0))
                throw new InvalidOperationException("only a fully installed non-terminal owner outside an install transaction may publish runtime reload behavior");
            if (ReloadOwnerInstallPublicationGate.HasPublicationAuthorityForRegression(false, false, 0))
                throw new InvalidOperationException("uninstalled/stale Harmony owner must remain inert even if runtime fields later become populated");
            if (ReloadOwnerInstallPublicationGate.HasPublicationAuthorityForRegression(false, true, 0))
                throw new InvalidOperationException("failed rollback with surviving stale owner must be permanently publication-inert");
            if (ReloadOwnerInstallPublicationGate.HasPublicationAuthorityForRegression(true, true, 0))
                throw new InvalidOperationException("terminal owner state must override any stale installed marker");
            if (ReloadOwnerInstallPublicationGate.HasPublicationAuthorityForRegression(true, false, 1))
                throw new InvalidOperationException("successful owner must still be inert while a FastAccess install transaction is active");

            ReloadOwnerInstallPublicationGate.ResetForRegression();
        }
    }
}
