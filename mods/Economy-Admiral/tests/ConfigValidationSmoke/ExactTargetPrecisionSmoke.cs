using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class ExactTargetPrecisionSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void MustPass(EconomyConfig config, string message)
        {
            try
            {
                EconomyConfigValidator.Validate(config);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Economy Admiral exact-target precision smoke: expected pass: {message}: {exception.Message}", exception);
            }
        }

        static void MustFail(EconomyConfig config, string message)
        {
            try
            {
                EconomyConfigValidator.Validate(config);
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException($"Economy Admiral exact-target precision smoke: expected failure: {message}");
        }

        MustPass(new EconomyConfig
        {
            QuestRewardOverrides = new Dictionary<string, ManualQuestRewardOverride>(StringComparer.Ordinal)
            {
                ["xp-exact"] = new() { ExperienceTarget = 1234 },
                ["standing-exact"] = new() { TraderStandingTarget = 0.1234 },
            },
        }, "integer XP and 4-decimal standing targets are exactly representable");

        MustFail(new EconomyConfig
        {
            QuestRewardOverrides = new Dictionary<string, ManualQuestRewardOverride>(StringComparer.Ordinal)
            {
                ["xp-fractional"] = new() { ExperienceTarget = 1234.5 },
            },
        }, "fractional XP target must not be silently rounded by enforcement");

        MustFail(new EconomyConfig
        {
            QuestRewardOverrides = new Dictionary<string, ManualQuestRewardOverride>(StringComparer.Ordinal)
            {
                ["standing-overprecision"] = new() { TraderStandingTarget = 0.12345 },
            },
        }, "standing target beyond transaction precision must not be silently rounded");
    }
}
