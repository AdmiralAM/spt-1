using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Admiral.SecondLife.Client
{
    internal sealed class RuntimeSpawnSelection
    {
        internal RuntimeSpawnSelection(object spawnPoint, object position, object rotation)
        {
            SpawnPoint = spawnPoint;
            Position = position;
            Rotation = rotation;
        }

        internal object SpawnPoint { get; }
        internal object Position { get; }
        internal object Rotation { get; }
    }

    internal static class RuntimeSafeSpawnSelector
    {
        const int MaximumSpawnPoints = 512;

        internal static bool TrySelect(
            Delegate playerFactory,
            object originalPlayer,
            object corpse,
            string seedIdentity,
            float minimumCorpseDistance,
            float minimumPlayerDistance,
            out RuntimeSpawnSelection selection,
            out string failure)
        {
            selection = null;
            failure = null;
            object spawnSystem = ReadField(playerFactory.Target, "spawnSystem");
            object spawnPoints = ReadField(spawnSystem, "_spawnPoints");
            object originalPoint = ReadProperty(originalPlayer, "SpawnPoint");
            object corpseTransform = ReadProperty(corpse, "transform");
            object corpsePosition = ReadProperty(corpseTransform, "position");
            if (!(spawnPoints is IEnumerable enumerable))
                return Fail("current map spawn collection is unavailable", out failure);
            if (corpsePosition == null)
                return Fail("native corpse position is unavailable", out failure);

            string originalId = ReadSpawnString(originalPoint, "Id");
            long originalSides = originalPoint == null ? 0 : ReadSpawnMask(originalPoint, "Sides");
            long originalCategories = originalPoint == null ? 0 : ReadSpawnMask(originalPoint, "Categories");
            string playerSide = ReadMember(originalPlayer, "Side")?.ToString();
            var candidates = new List<SpawnCandidate>();
            var pointsById = new Dictionary<string, object>(StringComparer.Ordinal);
            int visited = 0;
            int readable = 0;
            int maskPass = 0;
            int nativePass = 0;
            foreach (object point in enumerable)
            {
                if (++visited > MaximumSpawnPoints)
                    return Fail("map spawn collection exceeds the bounded limit", out failure);
                string id = ReadSpawnString(point, "Id");
                object position = ReadSpawnMember(point, "Position");
                if (string.IsNullOrWhiteSpace(id) || position == null) continue;
                readable++;
                object sides = ReadSpawnMember(point, "Sides");
                object categories = ReadSpawnMember(point, "Categories");
                long requiredSides = originalPoint == null ? ReadNamedMask(sides, playerSide) : originalSides;
                long requiredCategories = originalPoint == null ? ReadNamedMask(categories, "Player") : originalCategories;
                bool masksMatch = sides != null && categories != null && requiredSides != 0 && requiredCategories != 0 &&
                                  (Convert.ToInt64(sides) & requiredSides) != 0 &&
                                  (Convert.ToInt64(categories) & requiredCategories) != 0;
                if (masksMatch) maskPass++;
                bool nativeEligible = masksMatch && !ReadSpawnBoolean(point, "SpawnBlocked") && !ReadSpawnBoolean(point, "IsSnipeZone");
                if (nativeEligible) nativePass++;
                candidates.Add(new SpawnCandidate(id, ToWorldPoint(position), nativeEligible, false));
                pointsById[id] = point;
            }

            var combatPositions = new List<WorldPoint>();
            WorldPoint? killerPosition = null;
            object lastAggressor = ReadMember(originalPlayer, "LastAggressor");
            object lastAggressorPosition = ReadProperty(lastAggressor, "Position");
            if (lastAggressorPosition != null) killerPosition = ToWorldPoint(lastAggressorPosition);
            object gameWorld = ReadProperty(originalPlayer, "GameWorld");
            if (ReadMember(gameWorld, "RegisteredPlayers") is IEnumerable players)
            {
                foreach (object player in players)
                {
                    if (ReferenceEquals(player, originalPlayer)) continue;
                    object position = ReadProperty(player, "Position");
                    if (position != null) combatPositions.Add(ToWorldPoint(position));
                }
            }

            var configuredPolicy = new SafeSpawnPolicy(minimumPlayerDistance, minimumCorpseDistance, minimumPlayerDistance);
            SafeSpawnPolicy policy = SafeSpawnSelector.AdaptPolicyToMap(candidates, configuredPolicy);
            if (!SafeSpawnSelector.TrySelect(
                    candidates,
                    originalId,
                    ToWorldPoint(corpsePosition),
                    killerPosition,
                    combatPositions,
                    policy,
                    StableSeed(seedIdentity),
                    out SpawnCandidate selected,
                    out int safePass) ||
                !pointsById.TryGetValue(selected.Id, out object selectedPoint))
            {
                return Fail(
                    $"no bounded safe alternate spawn exists (visited={visited}, readable={readable}, mask-pass={maskPass}, native-pass={nativePass}, distance-pass={safePass}, " +
                    $"effective-distance corpse={policy.MinimumCorpseDistance:0.#}, killer={policy.MinimumKillerDistance:0.#}, combat={policy.MinimumCombatDistance:0.#})",
                    out failure);
            }

            selection = new RuntimeSpawnSelection(
                selectedPoint,
                ReadSpawnMember(selectedPoint, "Position"),
                ReadSpawnMember(selectedPoint, "Rotation"));
            return true;
        }

        internal static void Apply(object player, RuntimeSpawnSelection selection)
        {
            PropertyInfo spawnPoint = FindProperty(player.GetType(), "SpawnPoint");
            MethodInfo teleport = player.GetType().GetMethod(
                "Teleport",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { selection.Position.GetType(), typeof(bool) },
                null);
            if (spawnPoint?.SetMethod == null || teleport == null)
                throw new InvalidOperationException("new player cannot be bound to the selected spawn");
            spawnPoint.SetValue(player, selection.SpawnPoint, null);
            teleport.Invoke(player, new[] { selection.Position, (object)true });
            object transform = ReadProperty(ReadProperty(player, "Transform"), "Original");
            PropertyInfo rotation = transform?.GetType().GetProperty("rotation", BindingFlags.Instance | BindingFlags.Public);
            if (selection.Rotation != null && rotation?.CanWrite == true)
                rotation.SetValue(transform, selection.Rotation, null);
        }

        static int StableSeed(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty)
                    hash = (hash ^ character) * 16777619;
                return (int)hash;
            }
        }

        static WorldPoint ToWorldPoint(object vector) => new WorldPoint(
            Convert.ToSingle(FindField(vector.GetType(), "x")?.GetValue(vector)),
            Convert.ToSingle(FindField(vector.GetType(), "y")?.GetValue(vector)),
            Convert.ToSingle(FindField(vector.GetType(), "z")?.GetValue(vector)));

        static object ReadSpawnMember(object instance, string name)
        {
            if (instance == null) return null;
            foreach (Type contract in instance.GetType().GetInterfaces())
            {
                if (contract.FullName != "EFT.Game.Spawning.ISpawnPoint") continue;
                PropertyInfo property = contract.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (property != null) return property.GetValue(instance, null);
            }
            return ReadMember(instance, name);
        }

        static long ReadSpawnMask(object instance, string name) => Convert.ToInt64(ReadSpawnMember(instance, name));
        static bool ReadSpawnBoolean(object instance, string name) => ReadSpawnMember(instance, name) is bool value && value;
        static string ReadSpawnString(object instance, string name) => ReadSpawnMember(instance, name)?.ToString();
        static long ReadNamedMask(object mask, string name)
        {
            if (mask == null || string.IsNullOrWhiteSpace(name) || !mask.GetType().IsEnum) return 0;
            try { return Convert.ToInt64(Enum.Parse(mask.GetType(), name, true)); }
            catch (ArgumentException) { return 0; }
        }
        static object ReadMember(object instance, string name) => ReadProperty(instance, name) ?? ReadField(instance, name);
        static object ReadProperty(object instance, string name) => instance == null ? null : FindProperty(instance.GetType(), name)?.GetValue(instance, null);
        static object ReadField(object instance, string name) => instance == null ? null : FindField(instance.GetType(), name)?.GetValue(instance);

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

        static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
