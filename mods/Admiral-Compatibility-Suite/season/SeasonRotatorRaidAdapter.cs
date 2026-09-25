using System.Reflection;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.InRaid;

namespace AdmiralCompatibilitySuite.Seasons;

// Keep the upstream mod responsible for its initial season and weather setup.
// Only its date-based *subsequent* rotation is superseded while this adapter is loaded.
[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public sealed class SeasonRotatorRaidAdapter(
    WeatherConfig weatherConfig,
    ModHelper modHelper,
    IEnumerable<IRuntimePatch> patches,
    ISptLogger<SeasonRotatorRaidAdapter> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var upstream = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, "SeasonRotator", StringComparison.Ordinal));
        if (upstream is null)
            return Task.CompletedTask;
        if (upstream.GetName().Version != new Version(2, 0, 1, 0))
        {
            logger.Warning("Season Rotator raid adapter disabled: upstream is not the reviewed 2.0.1 build.");
            return Task.CompletedTask;
        }

        var matchingPatches = patches.OfType<SeasonRotatorEndRaidPatch>().Take(2).ToArray();
        if (matchingPatches.Length != 1)
        {
            logger.Warning("Season Rotator raid adapter disabled: end-raid patch is missing or ambiguous.");
            return Task.CompletedTask;
        }

        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var statePath = Path.Combine(modPath, "season-rotation-state.json");
        var upstreamPath = modHelper.GetAbsolutePathToModFolder(upstream);
        if (!SeasonRotationRuntime.Initialize(weatherConfig, statePath, Path.Combine(upstreamPath, "config.json"), logger))
            return Task.CompletedTask;

        try
        {
            matchingPatches[0].Enable();
            logger.Info("Season Rotator raid adapter active: next season after every 5 completed raids.");
        }
        catch (Exception exception)
        {
            SeasonRotationRuntime.Disable();
            logger.Error("Season Rotator raid adapter disabled: end-raid patch failed.", exception);
        }

        return Task.CompletedTask;
    }
}

[Injectable]
public sealed class SeasonRotatorEndRaidPatch : AbstractPatch
{
    protected override MethodBase? GetTargetMethod()
    {
        var matches = typeof(LocationLifecycleService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == "EndLocalRaidAsync"
                && method.ReturnType == typeof(Task)
                && method.GetParameters() is { Length: 3 } parameters
                && parameters[0].ParameterType == typeof(MongoId)
                && parameters[1].ParameterType == typeof(EndLocalRaidRequestData)
                && parameters[2].ParameterType == typeof(CancellationToken))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    [PatchPostfix]
    public static void Postfix(ref Task __result, EndLocalRaidRequestData request)
    {
        // Await the original task: failed raid-end processing must never advance the season.
        __result = CompleteAfterOriginal(__result, request.ServerId);
    }

    private static async Task CompleteAfterOriginal(Task original, string? serverId)
    {
        await original.ConfigureAwait(false);
        SeasonRotationRuntime.RecordCompletedRaid(serverId);
    }
}

internal static class SeasonRotationRuntime
{
    private static readonly object Sync = new();
    private static WeatherConfig? weather;
    private static string? stateFile;
    private static ISptLogger<SeasonRotatorRaidAdapter>? log;
    private static SeasonRotationState? state;
    private static int[] seasons = [];

    public static bool Initialize(WeatherConfig config, string path, string upstreamConfigPath, ISptLogger<SeasonRotatorRaidAdapter> logger)
    {
        lock (Sync)
        {
            try
            {
                using var upstreamConfig = JsonDocument.Parse(File.ReadAllText(upstreamConfigPath));
                // Upstream writes only the boot season's custom preset weights.
                // Reusing those weights after an in-process change is inconsistent.
                if (upstreamConfig.RootElement.GetProperty("UseCustomWeather").GetBoolean())
                    throw new InvalidDataException("Set Season Rotator UseCustomWeather=false for raid-based rotation.");
                var lengths = upstreamConfig.RootElement.GetProperty("SeasonsLengths");
                int[] enabled = SeasonCyclePolicy.UpstreamOrder.Where(season =>
                {
                    string name = Enum.GetName((Season)season)
                        ?? throw new InvalidDataException($"Unknown upstream season {season}.");
                    return lengths.GetProperty(name).GetInt32() > 0;
                }).ToArray();
                if (enabled.Length < 2)
                    throw new InvalidDataException("Season Rotator needs at least two enabled seasons.");

                SeasonRotationState loaded;
                if (File.Exists(path))
                {
                    loaded = JsonSerializer.Deserialize<SeasonRotationState>(File.ReadAllText(path))
                        ?? throw new InvalidDataException("Empty rotation state.");
                    if (loaded.Version != 1 || !enabled.Contains(loaded.Season)
                        || loaded.CompletedInSeason < 0 || loaded.CompletedInSeason >= SeasonCyclePolicy.RaidsPerSeason)
                        throw new InvalidDataException("Invalid rotation state; refusing to reset the counter.");
                }
                else
                {
                    int initial = (int?)config.OverrideSeason ?? enabled[0];
                    loaded = new SeasonRotationState(1, enabled.Contains(initial) ? initial : enabled[0], 0, null);
                    Save(path, loaded);
                }

                seasons = enabled;
                weather = config;
                stateFile = path;
                log = logger;
                state = loaded;
                config.OverrideSeason = (Season)loaded.Season;
                logger.Info($"Season Rotator raid state: {config.OverrideSeason}, {loaded.CompletedInSeason}/{SeasonCyclePolicy.RaidsPerSeason} raids.");
                return true;
            }
            catch (Exception exception)
            {
                logger.Error("Season Rotator raid adapter disabled: state could not be loaded or saved.", exception);
                Disable();
                return false;
            }
        }
    }

    public static void Disable()
    {
        lock (Sync)
        {
            weather = null;
            stateFile = null;
            log = null;
            state = null;
            seasons = [];
        }
    }

    public static void RecordCompletedRaid(string? serverId)
    {
        lock (Sync)
        {
            if (state is null || weather is null || stateFile is null)
                return;
            if (string.IsNullOrWhiteSpace(serverId))
            {
                log?.Warning("Season Rotator raid adapter skipped a raid with no server ID.");
                return;
            }
            var next = SeasonCyclePolicy.Advance(state, seasons, serverId);
            if (next is null) return;
            try
            {
                Save(stateFile, next);
                state = next;
                weather.OverrideSeason = (Season)next.Season;
                log?.Info($"Season Rotator raid state: {weather.OverrideSeason}, {next.CompletedInSeason}/{SeasonCyclePolicy.RaidsPerSeason} raids.");
            }
            catch (Exception exception)
            {
                log?.Error("Season Rotator raid adapter could not save state; rotation was not advanced.", exception);
            }
        }
    }

    private static void Save(string path, SeasonRotationState value)
    {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
