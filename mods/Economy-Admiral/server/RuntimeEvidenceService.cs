using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Path = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class RuntimeEvidenceService(
    TemplateTable templates,
    TradersTable traders,
    EconomyRuntimeConfigService runtimeConfigService,
    ModHelper modHelper,
    ISptLogger<RuntimeEvidenceService> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly string[] ExpectedReports =
    [
        "economy-admiral-audit.json",
        "economy-admiral-reward-utility.json",
        "economy-admiral-progression-graph.json",
        "economy-admiral-quest-constraints.json",
        "economy-admiral-quest-analysis.json",
        "economy-admiral-provenance-delta.json",
        "economy-admiral-enforcement-plan.json",
    ];

    private RuntimeFingerprint? before;

    public void CaptureBefore() => before = CaptureFingerprint();

    public async Task WriteAfterAsync(
        VanillaBaselineSnapshot vanillaBaseline,
        QuestProvenanceDeltaReport questProvenance,
        EnforcementPlanReport enforcement,
        CancellationToken cancellationToken)
    {
        if (before is null)
            throw new InvalidOperationException("Economy Admiral runtime evidence requires CaptureBefore() before the analysis pipeline.");

        var config = await runtimeConfigService.GetAsync(cancellationToken);
        var after = CaptureFingerprint();
        var modPath = modHelper.GetAbsolutePathToModFolder(typeof(RuntimeEvidenceService).Assembly);
        var buildIdentity = await ReadBuildIdentityAsync(modPath, cancellationToken);
        var reportDirectory = SafePath(modPath, "reports");
        var reportFiles = ExpectedReports.Select(fileName =>
        {
            var path = Path.Combine(reportDirectory, fileName);
            var exists = File.Exists(path);
            return new RuntimeReportEvidence
            {
                FileName = fileName,
                Exists = exists,
                SizeBytes = exists ? new FileInfo(path).Length : 0,
            };
        }).ToList();

        var databaseUnchanged = string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal);
        var allReportsPresent = reportFiles.All(report => report.Exists && report.SizeBytes > 0);
        var provenance = new RuntimeProvenanceEvidence
        {
            CapturePriority = vanillaBaseline.CapturePriority,
            PristineQuestCount = questProvenance.PristineQuestCount,
            FinalQuestCount = questProvenance.FinalQuestCount,
            ModAddedQuestCount = questProvenance.ModAddedQuestCount,
            PristineModifiedQuestCount = questProvenance.PristineModifiedQuestCount,
            PristineUnchangedQuestCount = questProvenance.PristineUnchangedQuestCount,
            RemovedPristineQuestCount = questProvenance.RemovedPristineQuestCount,
            PristineTraderCount = vanillaBaseline.TraderCount,
            FinalTraderCount = after.TraderCount,
            BaselineCaptured = vanillaBaseline.QuestCount > 0,
            CountsConsistent = questProvenance.PristineQuestCount == vanillaBaseline.QuestCount
                && questProvenance.FinalQuestCount == after.QuestCount
                && questProvenance.PristineModifiedQuestCount + questProvenance.PristineUnchangedQuestCount + questProvenance.RemovedPristineQuestCount == questProvenance.PristineQuestCount
                && questProvenance.ModAddedQuestCount + questProvenance.PristineModifiedQuestCount + questProvenance.PristineUnchangedQuestCount == questProvenance.FinalQuestCount,
        };
        var provenanceValid = provenance.BaselineCaptured && provenance.CountsConsistent;
        var enforcementValid = ValidateEnforcementEvidence(config, enforcement, databaseUnchanged);

        var manifest = new RuntimeEvidenceManifest
        {
            SchemaVersion = 5,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Mode = config.Mode.ToString(),
            Preset = config.Preset.ToString(),
            BuildIdentity = buildIdentity,
            Provenance = provenance,
            ExpectedReportCount = ExpectedReports.Length,
            PresentReportCount = reportFiles.Count(report => report.Exists && report.SizeBytes > 0),
            AllExpectedReportsPresent = allReportsPresent,
            DatabaseFingerprintBefore = before,
            DatabaseFingerprintAfter = after,
            DatabaseUnchangedAcrossPipeline = databaseUnchanged,
            DatabaseChangeExpected = config.Mode == EconomyMode.Enforce && enforcement.MutationCount > 0,
            ApplyMutations = enforcement.ApplyMutations,
            DeclaredMutationCount = enforcement.MutationCount,
            EnforcementEvidenceValid = enforcementValid,
            RuntimeGatePassed = allReportsPresent && provenanceValid && enforcementValid,
            Note = config.Mode == EconomyMode.Enforce
                ? config.EnableItemRewardStackNormalization
                    ? "Enforce runtime evidence: seven core reports; committed mutations may include Experience, TraderStanding, and the opt-in single-stack ItemRewardStackCount dimension. Item templates/records remain structural-protected. A zero-mutation Enforce run must leave the DB unchanged."
                    : "Enforce runtime evidence: seven core reports; a DB fingerprint change is valid only when the committed enforcement report declares one or more applied Experience/TraderStanding mutations. A zero-mutation Enforce run must leave the DB unchanged."
                : "Audit runtime evidence: seven core reports; DB must remain unchanged and the enforcement report may contain preview proposals but zero applied mutations.",
            Reports = reportFiles,
        };

        var manifestPath = SafePath(modPath, "reports/economy-admiral-runtime-evidence.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);

        if (!allReportsPresent)
        {
            logger.Warning($"[Economy Admiral] runtime evidence incomplete: {manifest.PresentReportCount}/{manifest.ExpectedReportCount} expected reports present; manifest={manifestPath}");
            return;
        }
        if (!provenanceValid)
        {
            logger.Error($"[Economy Admiral] runtime evidence FAILED: provenance counts inconsistent; pristine={provenance.PristineQuestCount}, final={provenance.FinalQuestCount}, added={provenance.ModAddedQuestCount}, modified={provenance.PristineModifiedQuestCount}, unchanged={provenance.PristineUnchangedQuestCount}, removed={provenance.RemovedPristineQuestCount}; manifest={manifestPath}");
            return;
        }
        if (!enforcementValid)
        {
            logger.Error($"[Economy Admiral] runtime evidence FAILED: mode={config.Mode}, fingerprintChanged={!databaseUnchanged}, planned={enforcement.PlannedMutationCount}, applied={enforcement.MutationCount}, rolledBack={enforcement.TransactionRolledBack}; manifest={manifestPath}");
            return;
        }

        logger.Info($"[Economy Admiral] runtime evidence PASS: mode={config.Mode}, fingerprintChanged={!databaseUnchanged}, mutations={enforcement.MutationCount}, {manifest.PresentReportCount}/{manifest.ExpectedReportCount} reports present; build={buildIdentity?.HeadSha ?? "local/unknown"}; manifest={manifestPath}");
    }

    private static bool ValidateEnforcementEvidence(EconomyConfig config, EnforcementPlanReport enforcement, bool databaseUnchanged)
    {
        if (enforcement.TransactionRolledBack || enforcement.MutationCount < 0 || enforcement.PlannedMutationCount < enforcement.MutationCount)
            return false;

        var applied = enforcement.Candidates.SelectMany(candidate => candidate.ProposedMutations).Where(mutation => mutation.Applied).ToList();
        if (applied.Count != enforcement.MutationCount)
            return false;

        var allowedDimensions = config.EnableItemRewardStackNormalization
            ? new HashSet<string>(["Experience", "TraderStanding", "ItemRewardStackCount"], StringComparer.Ordinal)
            : new HashSet<string>(["Experience", "TraderStanding"], StringComparer.Ordinal);
        if (applied.Any(mutation => !allowedDimensions.Contains(mutation.Dimension)))
            return false;

        if (enforcement.Candidates.Any(candidate => candidate.PristineUntouched && candidate.ProposedMutations.Any(mutation => mutation.Applied)))
            return false;

        foreach (var candidate in enforcement.Candidates.Where(candidate => candidate.ProvenanceClass == "PristineModified"))
        {
            foreach (var mutation in candidate.ProposedMutations.Where(mutation => mutation.Applied))
            {
                var provenDimension = mutation.Dimension switch
                {
                    "Experience" => "Experience",
                    "TraderStanding" => "TraderStanding",
                    "ItemRewardStackCount" => "SuccessItemHandbookValue",
                    _ => string.Empty,
                };
                if (string.IsNullOrEmpty(provenDimension) || !candidate.ChangedDimensions.Contains(provenDimension, StringComparer.Ordinal))
                    return false;
            }
        }

        if (config.Mode != EconomyMode.Enforce)
            return !enforcement.ApplyMutations && enforcement.MutationCount == 0 && databaseUnchanged;

        if (!enforcement.ApplyMutations)
            return false;
        if (enforcement.MutationCount == 0)
            return databaseUnchanged;

        return enforcement.TransactionCommitted && !databaseUnchanged;
    }

    private static async Task<RuntimeBuildIdentity?> ReadBuildIdentityAsync(string modPath, CancellationToken cancellationToken)
    {
        var path = SafePath(modPath, "BUILD_INFO.json");
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RuntimeBuildIdentity>(stream, JsonOptions, cancellationToken);
    }

    private RuntimeFingerprint CaptureFingerprint()
    {
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var lineCount = 0;
        void Add(string value)
        {
            incremental.AppendData(Encoding.UTF8.GetBytes(value));
            incremental.AppendData([0x0A]);
            lineCount++;
        }

        foreach (var item in templates.Items.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)) Add($"ITEM|{item.Key}");
        foreach (var handbook in templates.Handbook.Items.OrderBy(item => item.Id.ToString(), StringComparer.Ordinal)) Add($"HANDBOOK|{handbook.Id}|{handbook.Price}");
        foreach (var quest in templates.Quests.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            Add(
                $"QUEST|{quest.Key}|TRADER={quest.Value.TraderId}|RESTARTABLE={quest.Value.Restartable}|" +
                $"CONDITIONS={JsonSerializer.Serialize(quest.Value.Conditions)}|REWARDS={JsonSerializer.Serialize(quest.Value.Rewards)}"
            );
        }
        foreach (var trader in traders.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var traderId = trader.Key.ToString();
            foreach (var item in trader.Value.Assort.Items.OrderBy(item => item.Id.ToString(), StringComparer.Ordinal)) Add($"ASSORT_ITEM|{traderId}|{item.Id}|{item.Template}|{item.ParentId}");
            foreach (var barter in trader.Value.Assort.BarterScheme.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)) Add($"ASSORT_BARTER|{traderId}|{barter.Key}|{JsonSerializer.Serialize(barter.Value)}");
            foreach (var loyalty in trader.Value.Assort.LoyalLevelItems.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)) Add($"ASSORT_LOYALTY|{traderId}|{loyalty.Key}|{loyalty.Value}");
        }

        return new RuntimeFingerprint
        {
            Sha256 = Convert.ToHexString(incremental.GetHashAndReset()).ToLowerInvariant(),
            CanonicalLineCount = lineCount,
            TemplateItemCount = templates.Items.Count,
            HandbookItemCount = templates.Handbook.Items.Count,
            QuestCount = templates.Quests.Count,
            TraderCount = traders.Count,
            TraderAssortItemCount = traders.Values.Sum(trader => trader.Assort.Items.Count),
        };
    }

    private static string SafePath(string modPath, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(modPath, relativePath));
        var root = Path.GetFullPath(modPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Economy Admiral runtime evidence path must stay inside the mod directory.");
        return path;
    }
}

public sealed record RuntimeEvidenceManifest
{
    public required int SchemaVersion { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required string Mode { get; init; }
    public required string Preset { get; init; }
    public RuntimeBuildIdentity? BuildIdentity { get; init; }
    public required RuntimeProvenanceEvidence Provenance { get; init; }
    public required int ExpectedReportCount { get; init; }
    public required int PresentReportCount { get; init; }
    public required bool AllExpectedReportsPresent { get; init; }
    public required RuntimeFingerprint DatabaseFingerprintBefore { get; init; }
    public required RuntimeFingerprint DatabaseFingerprintAfter { get; init; }
    public required bool DatabaseUnchangedAcrossPipeline { get; init; }
    public required bool DatabaseChangeExpected { get; init; }
    public required bool ApplyMutations { get; init; }
    public required int DeclaredMutationCount { get; init; }
    public required bool EnforcementEvidenceValid { get; init; }
    public required bool RuntimeGatePassed { get; init; }
    public required string Note { get; init; }
    public required List<RuntimeReportEvidence> Reports { get; init; }
}

public sealed record RuntimeProvenanceEvidence
{
    public required int CapturePriority { get; init; }
    public required int PristineQuestCount { get; init; }
    public required int FinalQuestCount { get; init; }
    public required int ModAddedQuestCount { get; init; }
    public required int PristineModifiedQuestCount { get; init; }
    public required int PristineUnchangedQuestCount { get; init; }
    public required int RemovedPristineQuestCount { get; init; }
    public required int PristineTraderCount { get; init; }
    public required int FinalTraderCount { get; init; }
    public required bool BaselineCaptured { get; init; }
    public required bool CountsConsistent { get; init; }
}

public sealed record RuntimeBuildIdentity
{
    public required string Product { get; init; }
    public required string HeadSha { get; init; }
    public required string WorkflowRunId { get; init; }
    public required string ArtifactName { get; init; }
    public required string CompilePackageVersion { get; init; }
    public required string TargetRuntime { get; init; }
}

public sealed record RuntimeFingerprint
{
    public required string Sha256 { get; init; }
    public required int CanonicalLineCount { get; init; }
    public required int TemplateItemCount { get; init; }
    public required int HandbookItemCount { get; init; }
    public required int QuestCount { get; init; }
    public required int TraderCount { get; init; }
    public required int TraderAssortItemCount { get; init; }
}

public sealed record RuntimeReportEvidence
{
    public required string FileName { get; init; }
    public required bool Exists { get; init; }
    public required long SizeBytes { get; init; }
}
