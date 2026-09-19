using System;
using System.Collections.Generic;
using System.Linq;

namespace Admiral.SecondLife
{
    public sealed class LifeKill
    {
        public LifeKill(string identity, string name, string weapon, string bodyPart, float distance)
        {
            Identity = identity ?? string.Empty;
            Name = name ?? string.Empty;
            Weapon = weapon ?? string.Empty;
            BodyPart = bodyPart ?? string.Empty;
            Distance = Math.Max(0f, distance);
        }

        public string Identity { get; }
        public string Name { get; }
        public string Weapon { get; }
        public string BodyPart { get; }
        public float Distance { get; }
    }

    public sealed class LifeStatisticsSnapshot
    {
        public LifeStatisticsSnapshot(int experience, IReadOnlyList<LifeKill> kills)
        {
            Experience = Math.Max(0, experience);
            Kills = kills ?? Array.Empty<LifeKill>();
        }

        public int Experience { get; }
        public IReadOnlyList<LifeKill> Kills { get; }
    }

    public sealed class LifeStatisticsReport
    {
        LifeStatisticsReport(LifeStatisticsSnapshot firstLife, LifeStatisticsSnapshot secondLife)
        {
            FirstLife = firstLife;
            SecondLife = secondLife;
        }

        public LifeStatisticsSnapshot FirstLife { get; }
        public LifeStatisticsSnapshot SecondLife { get; }

        public static LifeStatisticsReport Separate(LifeStatisticsSnapshot firstLife, LifeStatisticsSnapshot finalSnapshot)
        {
            if (firstLife == null) throw new ArgumentNullException(nameof(firstLife));
            if (finalSnapshot == null) throw new ArgumentNullException(nameof(finalSnapshot));

            bool killsAreCumulative = finalSnapshot.Kills.Count >= firstLife.Kills.Count &&
                firstLife.Kills.Select(kill => kill.Identity)
                    .SequenceEqual(finalSnapshot.Kills.Take(firstLife.Kills.Count).Select(kill => kill.Identity), StringComparer.Ordinal);

            IReadOnlyList<LifeKill> secondKills = killsAreCumulative
                ? finalSnapshot.Kills.Skip(firstLife.Kills.Count).ToArray()
                : finalSnapshot.Kills.ToArray();
            int secondExperience = finalSnapshot.Experience >= firstLife.Experience
                ? finalSnapshot.Experience - firstLife.Experience
                : finalSnapshot.Experience;

            return new LifeStatisticsReport(
                firstLife,
                new LifeStatisticsSnapshot(secondExperience, secondKills));
        }
    }
}
