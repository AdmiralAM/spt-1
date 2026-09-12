using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Models.Common;
using SPTBeltArmbandInventory.Server;

internal static class DogtagCaseExcludedFilterNullParityRegression
{
    private sealed class NullPropertyShape
    {
        public IEnumerable<MongoId>? ExcludedFilter => null;
    }

    private sealed class NullFieldShape
    {
        public IEnumerable<MongoId>? ExcludedFilter = null;
    }

    private sealed class ValuePropertyShape
    {
        public IEnumerable<MongoId> ExcludedFilter { get; } = new[] { new MongoId("59f32bb586f774757e1e8442") };
    }

    [ModuleInitializer]
    internal static void Run()
    {
        MethodInfo? reader = typeof(DogtagCaseHostExclusionPolicy).GetMethod(
            "ReadOptionalExcludedFilter",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (reader == null)
            throw new InvalidOperationException("Dogtag ExcludedFilter null-parity regression failed: bounded optional-filter reader is missing.");

        if (Invoke(reader, new NullPropertyShape()) != null)
            throw new InvalidOperationException("Dogtag ExcludedFilter null-parity regression failed: readable property returning null must mean no optional exclusions.");
        if (Invoke(reader, new NullFieldShape()) != null)
            throw new InvalidOperationException("Dogtag ExcludedFilter null-parity regression failed: readable field returning null must mean no optional exclusions.");

        IEnumerable<MongoId>? values = Invoke(reader, new ValuePropertyShape());
        if (values == null)
            throw new InvalidOperationException("Dogtag ExcludedFilter null-parity regression failed: non-null property value was lost.");
        using IEnumerator<MongoId> enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext() || enumerator.Current.ToString() != "59f32bb586f774757e1e8442" || enumerator.MoveNext())
            throw new InvalidOperationException("Dogtag ExcludedFilter null-parity regression failed: non-null property content was not preserved exactly.");
    }

    private static IEnumerable<MongoId>? Invoke(MethodInfo reader, object shape)
    {
        try
        {
            return (IEnumerable<MongoId>?)reader.Invoke(null, new[] { shape });
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw new InvalidOperationException("Dogtag ExcludedFilter null-parity regression failed: optional-filter reader threw unexpectedly.", exception.InnerException);
        }
    }
}
