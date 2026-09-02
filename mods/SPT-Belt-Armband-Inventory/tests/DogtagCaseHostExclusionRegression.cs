using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Models.Common;
using SPTBeltArmbandInventory.Server;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class DogtagCaseHostExclusionRegression
    {
        [ModuleInitializer]
        internal static void Run()
        {
            var bear = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
            var usec = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
            var dogtagCase = new MongoId(RuntimeIdentity.DogtagCaseItemId);
            var foreign = new MongoId("5c093e3486f77430cb02e593");
            var included = new HashSet<MongoId> { bear, usec, dogtagCase, foreign };

            DogtagCaseHostExclusionPolicy.RequireEffectiveAcceptance(included, null);
            DogtagCaseHostExclusionPolicy.RequireEffectiveAcceptance(included, new HashSet<MongoId> { foreign });

            RequireThrows(() => DogtagCaseHostExclusionPolicy.RequireEffectiveAcceptance(
                new HashSet<MongoId> { usec, dogtagCase }, null),
                "missing BEAR effective acceptance must fail closed");
            RequireThrows(() => DogtagCaseHostExclusionPolicy.RequireEffectiveAcceptance(
                new HashSet<MongoId> { bear, dogtagCase }, null),
                "missing USEC effective acceptance must fail closed");
            RequireThrows(() => DogtagCaseHostExclusionPolicy.RequireEffectiveAcceptance(
                new HashSet<MongoId> { bear, usec }, null),
                "missing exact Dogtag Case effective acceptance must fail closed");

            RequireThrows(() => DogtagCaseHostExclusionPolicy.RequireEffectiveAcceptance(included, new HashSet<MongoId> { bear }),
                "ExcludedFilter may not negate BEAR acceptance");
            RequireThrows(() => DogtagCaseHostExclusionPolicy.RequireEffectiveAcceptance(included, new HashSet<MongoId> { usec }),
                "ExcludedFilter may not negate USEC acceptance");
            RequireThrows(() => DogtagCaseHostExclusionPolicy.RequireEffectiveAcceptance(included, new HashSet<MongoId> { dogtagCase }),
                "ExcludedFilter may not negate exact Dogtag Case acceptance");
        }

        static void RequireThrows(Action action, string message)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException("Dogtag host exclusion regression failed: " + message);
        }
    }
}
