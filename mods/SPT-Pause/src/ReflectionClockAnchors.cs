using System;
using System.Collections.Generic;
using System.Reflection;

namespace SPTPause
{
    public sealed class ReflectionClockAnchors
    {
        readonly List<Anchor> anchors;
        bool shifted;

        ReflectionClockAnchors(List<Anchor> anchors)
        {
            this.anchors = anchors;
        }

        public int Count { get { return anchors.Count; } }

        public static ReflectionClockAnchors CaptureNamedProperties(object target, params string[] propertyNames)
        {
            List<Anchor> result = new List<Anchor>();
            if (target == null || propertyNames == null) return new ReflectionClockAnchors(result);

            HashSet<FieldInfo> used = new HashSet<FieldInfo>();
            Type type = target.GetType();
            for (int i = 0; i < propertyNames.Length; i++)
            {
                PropertyInfo property = FindProperty(type, propertyNames[i]);
                if (property == null || property.GetIndexParameters().Length != 0) continue;
                object value;
                try { value = property.GetValue(target, null); }
                catch { continue; }
                FieldInfo field = FindMatchingField(target, type, property, value, used);
                if (field == null) continue;
                result.Add(new Anchor(target, field, field.GetValue(target)));
                used.Add(field);
            }
            return new ReflectionClockAnchors(result);
        }

        public static ReflectionClockAnchors CaptureDateTimeFields(object target, Type declaringType)
        {
            List<Anchor> result = new List<Anchor>();
            if (target == null || declaringType == null) return new ReflectionClockAnchors(result);
            FieldInfo[] fields = declaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                Type fieldType = fields[i].FieldType;
                if (fieldType != typeof(DateTime) && fieldType != typeof(DateTime?)) continue;
                object value;
                try { value = fields[i].GetValue(target); }
                catch { continue; }
                if (!HasMeaningfulDate(value)) continue;
                result.Add(new Anchor(target, fields[i], value));
            }
            return new ReflectionClockAnchors(result);
        }

        public static ReflectionClockAnchors CaptureFloatField(object target, string fieldName)
        {
            List<Anchor> result = new List<Anchor>();
            if (target == null || string.IsNullOrWhiteSpace(fieldName)) return new ReflectionClockAnchors(result);
            FieldInfo field = FindField(target.GetType(), fieldName);
            if (field != null && field.FieldType == typeof(float)) result.Add(new Anchor(target, field, field.GetValue(target)));
            return new ReflectionClockAnchors(result);
        }

        public void Shift(TimeSpan duration)
        {
            if (shifted) return;
            shifted = true;
            for (int i = 0; i < anchors.Count; i++) anchors[i].Shift(duration);
        }

        static bool HasMeaningfulDate(object value)
        {
            if (value is DateTime) return ((DateTime)value).Year >= 2000;
            return false;
        }

        static PropertyInfo FindProperty(Type type, string name)
        {
            while (type != null)
            {
                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null) return property;
                type = type.BaseType;
            }
            return null;
        }

        static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        static FieldInfo FindMatchingField(object target, Type type, PropertyInfo property, object value, HashSet<FieldInfo> used)
        {
            string backingName = "<" + property.Name + ">k__BackingField";
            FieldInfo exact = FindField(type, backingName);
            if (exact != null && !used.Contains(exact)) return exact;

            while (type != null)
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (used.Contains(field) || field.FieldType != property.PropertyType) continue;
                    object candidate;
                    try { candidate = field.GetValue(target); }
                    catch { continue; }
                    if (object.Equals(candidate, value)) return field;
                }
                type = type.BaseType;
            }
            return null;
        }

        sealed class Anchor
        {
            readonly object target;
            readonly FieldInfo field;
            readonly object value;

            public Anchor(object target, FieldInfo field, object value)
            {
                this.target = target;
                this.field = field;
                this.value = value;
            }

            public void Shift(TimeSpan duration)
            {
                try
                {
                    if (field.FieldType == typeof(DateTime)) field.SetValue(target, ((DateTime)value).Add(duration));
                    else if (field.FieldType == typeof(DateTime?))
                    {
                        DateTime? nullable = (DateTime?)value;
                        if (nullable.HasValue) field.SetValue(target, new DateTime?(nullable.Value.Add(duration)));
                    }
                    else if (field.FieldType == typeof(float)) field.SetValue(target, (float)value + (float)duration.TotalSeconds);
                }
                catch
                {
                    // The raid may have been disposed while paused. Restoration of Unity globals still proceeds.
                }
            }
        }
    }
}
