using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Admiral.SecondLife
{
    public sealed class PlayerDeathDiagnostic
    {
        public PlayerDeathDiagnostic(int lifeNumber, int deathNumber, string killerName, string killerRole, string killerSide, string weapon, string ammunition, float distance, string bodyPart, string damageType)
        {
            LifeNumber = Math.Max(1, lifeNumber);
            DeathNumber = Math.Max(1, deathNumber);
            KillerName = Normalize(killerName);
            KillerRole = Normalize(killerRole);
            KillerSide = Normalize(killerSide);
            Weapon = Normalize(weapon);
            Ammunition = Normalize(ammunition);
            Distance = Math.Max(0f, distance);
            BodyPart = Normalize(bodyPart);
            DamageType = Normalize(damageType);
        }

        public int LifeNumber { get; }
        public int DeathNumber { get; }
        public string KillerName { get; }
        public string KillerRole { get; }
        public string KillerSide { get; }
        public string Weapon { get; }
        public string Ammunition { get; }
        public float Distance { get; }
        public string BodyPart { get; }
        public string DamageType { get; }

        static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
    }

    public sealed class RaidDeathDiagnostics
    {
        readonly List<PlayerDeathDiagnostic> deaths = new List<PlayerDeathDiagnostic>(2);
        string raidId = "unknown";

        public IReadOnlyList<PlayerDeathDiagnostic> Deaths => deaths;

        public void Reset(string newRaidId)
        {
            raidId = string.IsNullOrWhiteSpace(newRaidId) ? "unknown" : newRaidId.Trim();
            deaths.Clear();
        }

        public PlayerDeathDiagnostic Record(string killerName, string killerRole, string killerSide, string weapon, string ammunition, float distance, string bodyPart, string damageType)
        {
            int ordinal = deaths.Count + 1;
            var captured = new PlayerDeathDiagnostic(ordinal, ordinal, killerName, killerRole, killerSide, weapon, ammunition, distance, bodyPart, damageType);
            deaths.Add(captured);
            return captured;
        }

        public string Format(PlayerDeathDiagnostic death)
        {
            if (death == null) throw new ArgumentNullException(nameof(death));
            return new StringBuilder(256)
                .Append("SecondLifeDeath{")
                .Append("\"raid\":\"").Append(Escape(raidId)).Append("\",")
                .Append("\"life\":").Append(death.LifeNumber).Append(',')
                .Append("\"death\":").Append(death.DeathNumber).Append(',')
                .Append("\"killerName\":\"").Append(Escape(death.KillerName)).Append("\",")
                .Append("\"killerRole\":\"").Append(Escape(death.KillerRole)).Append("\",")
                .Append("\"killerSide\":\"").Append(Escape(death.KillerSide)).Append("\",")
                .Append("\"weapon\":\"").Append(Escape(death.Weapon)).Append("\",")
                .Append("\"ammo\":\"").Append(Escape(death.Ammunition)).Append("\",")
                .Append("\"distanceMeters\":").Append(death.Distance.ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
                .Append("\"bodyPart\":\"").Append(Escape(death.BodyPart)).Append("\",")
                .Append("\"damageType\":\"").Append(Escape(death.DamageType)).Append("\"}")
                .ToString();
        }

        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
