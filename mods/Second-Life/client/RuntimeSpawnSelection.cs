using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Admiral.SecondLife.Client
{
    internal sealed class RuntimeSpawnSelection
    {
        internal RuntimeSpawnSelection(object spawnPoint, object position)
        {
            SpawnPoint = spawnPoint;
            Position = position;
        }

        internal object SpawnPoint { get; }
        internal object Position { get; }
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
            if (!(spawnPoints is IEnumerable enumerable) || originalPoint == null || corpsePosition == null)
                return Fail("current map spawn collection or corpse position is unavailable", out failure);

            string originalId = ReadString(originalPoint, "Id");
            long originalSides = ReadMask(originalPoint, "Sides");
            long originalCategories = ReadMask(originalPoint, "Categories");
            var candidates = new List<SpawnCandidate>();
            var pointsById = new Dictionary<string, object>(StringComparer.Ordinal);
            int visited = 0;
            foreach (object point in enumerable)
            {
                if (++visited > MaximumSpawnPoints)
                    return Fail("map spawn collection exceeds the bounded limit", out failure);
                string id = ReadString(point, "Id");
                object position = ReadProperty(point, "Position");
                if (string.IsNullOrWhiteSpace(id) || position == null) continue;
                bool masksMatch = (ReadMask(point, "Sides") & originalSides) != 0 &&
                                  (ReadMask(point, "Categories") & originalCategories) != 0;
                bool nativeEligible = masksMatch && !ReadBoolean(point, "SpawnBlocked") && !ReadBoolean(point, "IsSnipeZone");
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

            var policy = new SafeSpawnPolicy(minimumPlayerDistance, minimumCorpseDistance, minimumPlayerDistance);
            if (!SafeSpawnSelector.TrySelect(
                    candidates,
                    originalId,
                    ToWorldPoint(corpsePosition),
                    killerPosition,
                    combatPositions,
                    policy,
                    StableSeed(seedIdentity),
                    out SpawnCandidate selected) ||
                !pointsById.TryGetValue(selected.Id, out object selectedPoint))
            {
                return Fail("no bounded safe alternate spawn exists", out failure);
            }

            selection = new RuntimeSpawnSelection(selectedPoint, ReadProperty(selectedPoint, "Position"));
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

        static long ReadMask(object instance, string property) => Convert.ToInt64(ReadProperty(instance, property));
        static bool ReadBoolean(object instance, string property) => ReadProperty(instance, property) is bool value && value;
        static string ReadString(object instance, string property) => ReadProperty(instance, property)?.ToString();
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
