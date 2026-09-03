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
                throw new InvalidOperationException("extra finalization must fail closed against depth underflow and leave the idle gate open");

            ReloadOwnerInstallPublicationGate.ResetForRegression();
        }
    }
}
