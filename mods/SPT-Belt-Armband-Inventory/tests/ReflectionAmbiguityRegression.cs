using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReflectionAmbiguityRegression
{
    class BaseProbe
    {
        public string Value => "base";
        public virtual string VirtualValue => "base-virtual";
    }

    sealed class DerivedProbe : BaseProbe
    {
        public new string Value => "derived";
        public override string VirtualValue => "derived-virtual";
    }

    [ModuleInitializer]
    internal static void Run()
    {
        var probe = new DerivedProbe();
        bool legacyAmbiguous = false;
        try
        {
            typeof(DerivedProbe).GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        catch (AmbiguousMatchException)
        {
            legacyAmbiguous = true;
        }

        if (!legacyAmbiguous)
            throw new InvalidOperationException("Regression fixture no longer reproduces AmbiguousMatchException; update the fixture before trusting this gate.");

        object value = ReflectionTools.ReadMember(probe, "Value");
        if (!Equals(value, "derived"))
            throw new InvalidOperationException("Ambiguity-safe reflection must deterministically select the nearest declared member.");

        object virtualValue = ReflectionTools.ReadMember(probe, "VirtualValue");
        if (!Equals(virtualValue, "derived-virtual"))
            throw new InvalidOperationException("Ambiguity-safe reflection must preserve normal overridden-property behavior.");

        if (ReflectionTools.FindInstanceProperty(typeof(DerivedProbe), "Value")?.DeclaringType != typeof(DerivedProbe))
            throw new InvalidOperationException("FindInstanceProperty must select the nearest declared property without Type.GetProperty ambiguity.");

        if (ReflectionTools.ReadMember(probe, "Missing") != null)
            throw new InvalidOperationException("Missing optional members must fail closed.");
    }
}
