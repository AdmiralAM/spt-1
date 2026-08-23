using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace SPTItemIntelligence
{
    public interface IItemHoverAnchorSink
    {
        void SetAnchor(object itemView);
        void ClearAnchor();
    }

    public interface IItemViewRegistrySink
    {
        void RegisterView(object itemView, string templateId);
        void UnregisterView(object itemView);
        void ClearViews();
    }

    public static class EftItemTemplateIdResolver
    {
        static readonly string[] itemMembers = { "Item", "item", "ItemContext", "itemContext", "_item" };
        static readonly string[] templateIdMembers = { "TemplateId", "templateId", "Tpl", "tpl", "_tpl" };
        static readonly string[] templateMembers = { "Template", "template", "_template" };
        static readonly string[] templateObjectIdMembers = { "TemplateId", "templateId", "Id", "id", "_id", "Tpl", "tpl", "_tpl" };
        static readonly string[] stackMembers = { "StackObjectsCount", "stackObjectsCount", "StackCount", "stackCount" };
        static readonly string[] updateMembers = { "Upd", "upd", "Update", "update" };
        static readonly object cacheSync = new object();
        static readonly Dictionary<MemberCacheKey, MemberInfo> memberCache = new Dictionary<MemberCacheKey, MemberInfo>();

        public static string Resolve(object itemViewOrItem)
        {
            if (itemViewOrItem == null) return string.Empty;

            object item = ReadFirst(itemViewOrItem, itemMembers) ?? itemViewOrItem;
            string direct = ReadString(item, templateIdMembers);
            if (!string.IsNullOrWhiteSpace(direct)) return Normalize(direct);

            object template = ReadFirst(item, templateMembers);
            string nested = ReadString(template, templateObjectIdMembers);
            return Normalize(nested);
        }

        public static int ResolveStackCount(object itemViewOrItem)
        {
            if (itemViewOrItem == null) return 1;
            object item = ReadFirst(itemViewOrItem, itemMembers) ?? itemViewOrItem;
            object value = ReadFirst(item, stackMembers);
            if (value == null) value = ReadFirst(ReadFirst(item, updateMembers), stackMembers);
            if (value == null) return 1;
            try { return Math.Max(1, Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)); }
            catch { return 1; }
        }

        static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        static string ReadString(object source, string[] names)
        {
            object value = ReadFirst(source, names);
            return value == null ? null : value.ToString();
        }

        static object ReadFirst(object source, string[] names)
        {
            if (source == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                object value;
                if (TryRead(source, names[i], out value) && value != null) return value;
            }
            return null;
        }

        static bool TryRead(object source, string name, out object value)
        {
            IDictionary dictionary = source as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!string.Equals(entry.Key == null ? null : entry.Key.ToString(), name, StringComparison.OrdinalIgnoreCase)) continue;
                    value = entry.Value;
                    return true;
                }
                value = null;
                return false;
            }

            Type type = source.GetType();
            MemberInfo member = GetMember(type, name);
            try
            {
                PropertyInfo property = member as PropertyInfo;
                if (property != null)
                {
                    value = property.GetValue(source, null);
                    return true;
                }

                FieldInfo field = member as FieldInfo;
                if (field != null)
                {
                    value = field.GetValue(source);
                    return true;
                }
            }
            catch
            {
                // An obfuscated/game accessor is allowed to fail. Other aliases remain available.
            }

            value = null;
            return false;
        }

        static MemberInfo GetMember(Type type, string name)
        {
            MemberCacheKey key = new MemberCacheKey(type, name);
            lock (cacheSync)
            {
                MemberInfo cached;
                if (memberCache.TryGetValue(key, out cached)) return cached;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
                MemberInfo member = null;
                try
                {
                    PropertyInfo property = type.GetProperty(name, flags);
                    if (property != null && property.GetIndexParameters().Length == 0) member = property;
                }
                catch { }
                if (member == null)
                {
                    try { member = type.GetField(name, flags); }
                    catch { }
                }
                memberCache[key] = member;
                return member;
            }
        }

        struct MemberCacheKey : IEquatable<MemberCacheKey>
        {
            readonly Type type;
            readonly string name;

            public MemberCacheKey(Type type, string name)
            {
                this.type = type;
                this.name = name;
            }

            public bool Equals(MemberCacheKey other)
            {
                return type == other.type && string.Equals(name, other.name, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is MemberCacheKey && Equals((MemberCacheKey)obj);
            }

            public override int GetHashCode()
            {
                return ((type == null ? 0 : type.GetHashCode()) * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(name ?? string.Empty);
            }
        }
    }

    public sealed class EftItemViewHoverIntegration : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.itemintelligence.hover";
        static EftItemViewHoverIntegration active;

        readonly ItemHoverRuntimeController controller;
        readonly IItemHoverAnchorSink anchorSink;
        readonly IItemViewRegistrySink registrySink;
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        object activeItemView;
        MethodInfo unpatchSelf;
        bool disposed;
        int unresolvedTemplateReported;

        public EftItemViewHoverIntegration(
            ItemHoverRuntimeController controller,
            Action<string> logInfo = null,
            Action<string> logWarning = null,
            IItemHoverAnchorSink anchorSink = null,
            IItemViewRegistrySink registrySink = null)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.logInfo = logInfo;
            this.logWarning = logWarning;
            this.anchorSink = anchorSink;
            this.registrySink = registrySink;
        }

        public bool IsInstalled { get; private set; }
        public int PatchedMethodCount { get; private set; }

        public bool TryInstall()
        {
            if (disposed || IsInstalled) return IsInstalled;

            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                if (harmonyType == null || harmonyMethodType == null)
                    return Unavailable("Harmony is unavailable; Item Intelligence hover integration remains disabled.");

                List<HoverPatchTarget> targets = DiscoverTargets(AppDomain.CurrentDomain.GetAssemblies());
                if (targets.Count == 0)
                    return Unavailable("EFT ItemView pointer methods were not found; hover integration remains disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                if (harmony == null || patchMethod == null)
                    return Unavailable("Harmony patch API is incompatible; hover integration remains disabled.");

                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (harmonyMethodConstructor == null)
                    return Unavailable("HarmonyMethod constructor is unavailable; hover integration remains disabled.");

                MethodInfo enterPostfix = typeof(EftItemViewHoverIntegration).GetMethod(nameof(HoverEnterPostfix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo exitPostfix = typeof(EftItemViewHoverIntegration).GetMethod(nameof(HoverExitPostfix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo initPostfix = typeof(EftItemViewHoverIntegration).GetMethod(nameof(ItemViewInitPostfix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo killPrefix = typeof(EftItemViewHoverIntegration).GetMethod(nameof(ItemViewKillPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                object enterPatch = harmonyMethodConstructor.Invoke(new object[] { enterPostfix });
                object exitPatch = harmonyMethodConstructor.Invoke(new object[] { exitPostfix });
                object initPatch = harmonyMethodConstructor.Invoke(new object[] { initPostfix });
                object killPatch = harmonyMethodConstructor.Invoke(new object[] { killPrefix });

                Interlocked.Exchange(ref active, this);
                for (int i = 0; i < targets.Count; i++)
                {
                    Patch(patchMethod, targets[i].Enter, harmonyMethodType, enterPatch, false);
                    Patch(patchMethod, targets[i].Exit, harmonyMethodType, exitPatch, false);
                    Patch(patchMethod, targets[i].Initialize, harmonyMethodType, initPatch, false);
                    Patch(patchMethod, targets[i].Kill, harmonyMethodType, killPatch, true);
                    PatchedMethodCount += 4;
                }

                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                IsInstalled = PatchedMethodCount > 0;
                if (IsInstalled && logInfo != null) logInfo("Item Intelligence hover integration installed on " + PatchedMethodCount + " EFT ItemView methods.");
                return IsInstalled;
            }
            catch (Exception exception)
            {
                SafeUnpatch();
                return Unavailable("Item Intelligence hover integration failed safely: " + exception.Message);
            }
        }

        internal bool DispatchEnter(object itemView)
        {
            if (disposed) return false;
            string templateId = EftItemTemplateIdResolver.Resolve(itemView);
            if (templateId.Length == 0)
            {
                Interlocked.Exchange(ref activeItemView, null);
                if (anchorSink != null) anchorSink.ClearAnchor();
                controller.OnHoverExit();
                if (Interlocked.Exchange(ref unresolvedTemplateReported, 1) == 0 && logWarning != null)
                    logWarning("Item Intelligence could not resolve a template id from an EFT ItemView; this shape is ignored.");
                return false;
            }

            if (registrySink != null) registrySink.RegisterView(itemView, templateId);
            Interlocked.Exchange(ref activeItemView, itemView);
            if (anchorSink != null) anchorSink.SetAnchor(itemView);
            controller.OnHoverEnter(templateId);
            return true;
        }

        internal bool DispatchRegister(object itemView)
        {
            if (disposed || registrySink == null) return false;
            string templateId = EftItemTemplateIdResolver.Resolve(itemView);
            if (templateId.Length == 0) return false;
            registrySink.RegisterView(itemView, templateId);
            return true;
        }

        internal void DispatchUnregister(object itemView)
        {
            if (disposed || itemView == null) return;
            if (registrySink != null) registrySink.UnregisterView(itemView);
            if (object.ReferenceEquals(Volatile.Read(ref activeItemView), itemView)) DispatchExit(itemView);
        }

        internal void DispatchExit(object itemView = null)
        {
            if (disposed) return;
            object activeView = Volatile.Read(ref activeItemView);
            if (itemView != null && activeView != null && !object.ReferenceEquals(activeView, itemView)) return;
            Interlocked.Exchange(ref activeItemView, null);
            if (anchorSink != null) anchorSink.ClearAnchor();
            controller.OnHoverExit();
        }

        internal static List<HoverPatchTarget> DiscoverTargets(IEnumerable<Assembly> assemblies)
        {
            List<HoverPatchTarget> result = new List<HoverPatchTarget>();
            HashSet<MethodInfo> seenEnter = new HashSet<MethodInfo>();
            if (assemblies == null) return result;

            foreach (Assembly assembly in assemblies)
            {
                Type[] types = GetLoadableTypes(assembly);
                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null || !LooksLikeItemView(type)) continue;

                    MethodInfo enter = FindPointerMethod(type, "OnPointerEnter");
                    MethodInfo exit = FindPointerMethod(type, "OnPointerExit");
                    MethodInfo initialize = FindParameterlessMethod(type, "Init");
                    MethodInfo kill = FindParameterlessMethod(type, "Kill");
                    if (enter == null || exit == null || initialize == null || kill == null || !seenEnter.Add(enter)) continue;
                    result.Add(new HoverPatchTarget(type, enter, exit, initialize, kill));
                }
            }
            return result;
        }

        static bool LooksLikeItemView(Type type)
        {
            if (type.Name.EndsWith("ItemView", StringComparison.Ordinal)) return true;
            Type current = type.BaseType;
            while (current != null)
            {
                if (current.Name == "ItemView") return true;
                current = current.BaseType;
            }
            return false;
        }

        static MethodInfo FindPointerMethod(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods;
            try { methods = type.GetMethods(flags); }
            catch { return null; }
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name == name && method.GetParameters().Length == 1) return method;
            }
            return null;
        }

        static MethodInfo FindParameterlessMethod(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods;
            try { methods = type.GetMethods(flags); }
            catch { return null; }
            for (int i = 0; i < methods.Length; i++)
                if (methods[i].Name == name && methods[i].GetParameters().Length == 0) return methods[i];
            return null;
        }

        static Type[] GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null) return new Type[0];
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { return exception.Types ?? new Type[0]; }
            catch { return new Type[0]; }
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "Patch") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 3 || !typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType)) continue;
                for (int p = 1; p < parameters.Length; p++)
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, MethodInfo original, Type harmonyMethodType, object patch, bool prefix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            string patchParameter = prefix ? "prefix" : "postfix";
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, patchParameter, StringComparison.OrdinalIgnoreCase)) arguments[i] = patch;
            patchMethod.Invoke(harmony, arguments);
        }

        bool Unavailable(string message)
        {
            IsInstalled = false;
            PatchedMethodCount = 0;
            if (logWarning != null) logWarning(message);
            return false;
        }

        static void HoverEnterPostfix(object __instance)
        {
            EftItemViewHoverIntegration instance = Volatile.Read(ref active);
            if (instance == null) return;
            try { instance.DispatchEnter(__instance); }
            catch (Exception exception)
            {
                if (instance.logWarning != null) instance.logWarning("Item Intelligence hover enter failed safely: " + exception.Message);
            }
        }

        static void HoverExitPostfix(object __instance)
        {
            EftItemViewHoverIntegration instance = Volatile.Read(ref active);
            if (instance == null) return;
            try { instance.DispatchExit(__instance); }
            catch (Exception exception)
            {
                if (instance.logWarning != null) instance.logWarning("Item Intelligence hover exit failed safely: " + exception.Message);
            }
        }

        static void ItemViewInitPostfix(object __instance)
        {
            EftItemViewHoverIntegration instance = Volatile.Read(ref active);
            if (instance == null) return;
            try { instance.DispatchRegister(__instance); }
            catch (Exception exception)
            {
                if (instance.logWarning != null) instance.logWarning("Item Intelligence ItemView registration failed safely: " + exception.Message);
            }
        }

        static void ItemViewKillPrefix(object __instance)
        {
            EftItemViewHoverIntegration instance = Volatile.Read(ref active);
            if (instance == null) return;
            try { instance.DispatchUnregister(__instance); }
            catch (Exception exception)
            {
                if (instance.logWarning != null) instance.logWarning("Item Intelligence ItemView cleanup failed safely: " + exception.Message);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Interlocked.Exchange(ref activeItemView, null);
            if (anchorSink != null) anchorSink.ClearAnchor();
            if (registrySink != null) registrySink.ClearViews();
            controller.OnHoverExit();
            SafeUnpatch();
        }

        void SafeUnpatch()
        {
            if (object.ReferenceEquals(Volatile.Read(ref active), this)) Interlocked.Exchange(ref active, null);
            try
            {
                if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null);
            }
            catch { }
            harmony = null;
            unpatchSelf = null;
            IsInstalled = false;
            PatchedMethodCount = 0;
        }

        internal sealed class HoverPatchTarget
        {
            public HoverPatchTarget(Type type, MethodInfo enter, MethodInfo exit, MethodInfo initialize, MethodInfo kill)
            {
                Type = type;
                Enter = enter;
                Exit = exit;
                Initialize = initialize;
                Kill = kill;
            }

            public Type Type { get; }
            public MethodInfo Enter { get; }
            public MethodInfo Exit { get; }
            public MethodInfo Initialize { get; }
            public MethodInfo Kill { get; }
        }
    }
}
