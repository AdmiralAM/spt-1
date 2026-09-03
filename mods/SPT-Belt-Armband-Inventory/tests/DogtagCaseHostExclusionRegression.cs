using System;
using System.Collections.Generic;
using System.Reflection;
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

            MethodInfo reader = typeof(DogtagCaseHostExclusionPolicy).GetMethod(
                "ReadOptionalExcludedFilter",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Dogtag host exclusion regression failed: bounded optional-member reader is missing");

            if (reader.Invoke(null, new object[] { new NoExcludedMember() }) != null)
                throw new InvalidOperationException("Dogtag host exclusion regression failed: absent future ExcludedFilter must remain a null/no-op contract");

            var propertyOnly = (IEnumerable<MongoId>?)reader.Invoke(null, new object[] { new PropertyOnlyExcluded(foreign) });
            if (propertyOnly == null || !new HashSet<MongoId>(propertyOnly).SetEquals(new[] { foreign }))
                throw new InvalidOperationException("Dogtag host exclusion regression failed: unique public ExcludedFilter property was not read exactly");

            var fieldOnly = (IEnumerable<MongoId>?)reader.Invoke(null, new object[] { new FieldOnlyExcluded(foreign) });
            if (fieldOnly == null || !new HashSet<MongoId>(fieldOnly).SetEquals(new[] { foreign }))
                throw new InvalidOperationException("Dogtag host exclusion regression failed: unique public ExcludedFilter field was not read exactly");

            var privateProperty = (IEnumerable<MongoId>?)reader.Invoke(null, new object[] { new PrivatePropertyExcluded(foreign) });
            if (privateProperty == null || !new HashSet<MongoId>(privateProperty).SetEquals(new[] { foreign }))
                throw new InvalidOperationException("Dogtag host exclusion regression failed: unique non-public ExcludedFilter property was silently treated as absent");

            var privateField = (IEnumerable<MongoId>?)reader.Invoke(null, new object[] { new PrivateFieldExcluded(foreign) });
            if (privateField == null || !new HashSet<MongoId>(privateField).SetEquals(new[] { foreign }))
                throw new InvalidOperationException("Dogtag host exclusion regression failed: unique non-public ExcludedFilter field was silently treated as absent");

            RequireReflectionThrows(reader, new AmbiguousExcluded(foreign),
                "property/field hiding collision must fail closed instead of preferring one future ExcludedFilter authority");
            RequireReflectionThrows(reader, new PrivateAmbiguousExcluded(foreign),
                "non-public hierarchy collision must fail closed instead of preferring one future ExcludedFilter authority");
            RequireReflectionThrows(reader, new UnsupportedExcluded(),
                "non-MongoId future ExcludedFilter enumerable must fail closed");
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

        static void RequireReflectionThrows(MethodInfo reader, object target, string message)
        {
            try
            {
                reader.Invoke(null, new[] { target });
            }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException("Dogtag host exclusion regression failed: " + message);
        }

        private sealed class NoExcludedMember { }

        private sealed class PropertyOnlyExcluded
        {
            public IEnumerable<MongoId> ExcludedFilter { get; }
            internal PropertyOnlyExcluded(MongoId id) => ExcludedFilter = new[] { id };
        }

        private sealed class FieldOnlyExcluded
        {
            public IEnumerable<MongoId> ExcludedFilter;
            internal FieldOnlyExcluded(MongoId id) => ExcludedFilter = new[] { id };
        }

        private sealed class PrivatePropertyExcluded
        {
            private IEnumerable<MongoId> ExcludedFilter { get; }
            internal PrivatePropertyExcluded(MongoId id) => ExcludedFilter = new[] { id };
        }

        private sealed class PrivateFieldExcluded
        {
            private IEnumerable<MongoId> ExcludedFilter;
            internal PrivateFieldExcluded(MongoId id) => ExcludedFilter = new[] { id };
        }

        private class AmbiguousExcludedBase
        {
            public IEnumerable<MongoId> ExcludedFilter;
            protected AmbiguousExcludedBase(MongoId id) => ExcludedFilter = new[] { id };
        }

        private sealed class AmbiguousExcluded : AmbiguousExcludedBase
        {
            public new IEnumerable<MongoId> ExcludedFilter { get; }
            internal AmbiguousExcluded(MongoId id) : base(id) => ExcludedFilter = new[] { id };
        }

        private class PrivateAmbiguousExcludedBase
        {
            private IEnumerable<MongoId> ExcludedFilter;
            protected PrivateAmbiguousExcludedBase(MongoId id) => ExcludedFilter = new[] { id };
        }

        private sealed class PrivateAmbiguousExcluded : PrivateAmbiguousExcludedBase
        {
            private new IEnumerable<MongoId> ExcludedFilter { get; }
            internal PrivateAmbiguousExcluded(MongoId id) : base(id) => ExcludedFilter = new[] { id };
        }

        private sealed class UnsupportedExcluded
        {
            public string ExcludedFilter => "not-a-MongoId-enumerable";
        }
    }
}