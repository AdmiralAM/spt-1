using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class EffectiveQuestGateSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void MustFail(string name, Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { Console.WriteLine($"PASS {name}"); return; }
            throw new InvalidOperationException($"Expected '{name}' to fail.");
        }

        var sidearms = new[]
        {
            new QuestGateNode
            {
                QuestId = "59ca4829e098dfafa03888d2",
                LevelRequirement = 5,
            },
            new QuestGateNode
            {
                QuestId = "b016df9d2bea4269cc59d531",
                LevelRequirement = 8,
                PrerequisiteQuestIds = new[] { "59ca4829e098dfafa03888d2" },
            },
            new QuestGateNode
            {
                QuestId = "8cba3e2ec639a4aa2c26c4da",
                LevelRequirement = 12,
                PrerequisiteQuestIds = new[] { "b016df9d2bea4269cc59d531" },
            },
        };

        var resolved = EffectiveQuestGateEvidenceResolver.Resolve("8cba3e2ec639a4aa2c26c4da", sidearms);
        Require(resolved.MaximumPrerequisiteDepth == 2, "Sidearms Munitions depth must include Qualification -> Fieldwork -> Munitions.");
        Require(resolved.EffectiveMinimumLevel == 12, "Effective gate must use authored quest-chain level constraints, not LL1 trader metadata.");
        Require(resolved.KnownLevelConstraintCount == 3, "All three sidearms stages carry explicit level evidence.");
        Require(resolved.CompleteQuestGraphEvidence, "Complete sidearms fixture should remain complete.");

        MustFail("missing prerequisite", () => EffectiveQuestGateEvidenceResolver.Resolve(
            "q2",
            new[]
            {
                new QuestGateNode { QuestId = "q2", LevelRequirement = 10, PrerequisiteQuestIds = new[] { "missing" } },
            }
        ));

        MustFail("cycle", () => EffectiveQuestGateEvidenceResolver.Resolve(
            "q1",
            new[]
            {
                new QuestGateNode { QuestId = "q1", LevelRequirement = 5, PrerequisiteQuestIds = new[] { "q2" } },
                new QuestGateNode { QuestId = "q2", LevelRequirement = 10, PrerequisiteQuestIds = new[] { "q1" } },
            }
        ));

        MustFail("invalid level", () => EffectiveQuestGateEvidenceResolver.Resolve(
            "q1",
            new[] { new QuestGateNode { QuestId = "q1", LevelRequirement = 0 } }
        ));

        Console.WriteLine("Economy Admiral effective quest gate smoke PASS");
    }
}
