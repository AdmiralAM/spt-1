using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Registers a dedicated Dogtag-slot container without replacing the vanilla
/// player dogtag contract. The container is cloned from EFT's own Dogtag Case,
/// and its single internal grid copies the source case's exact filter groups so
/// B&A&HB never broadens what can be stored inside it.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 3)]
public sealed class DogtagCaseItem(
    TemplateTable templateTable,
    CustomItemService customItemService,
    ISptLogger<DogtagCaseItem> logger) : IOnLoad
{
    public const string TemplateId = RuntimeIdentity.DogtagCaseItemId;
    public const string GridId = RuntimeIdentity.DogtagCaseGridId;

    private static readonly MongoId SourceDogtagCaseTpl = new("5c093e3486f77430cb02e593");
    private static readonly MongoId DogtagCaseTpl = new(TemplateId);
    private static readonly MongoId DefaultInventoryTpl = RuntimeCandidateBeltItem.DefaultInventoryTpl;
    private const string DogtagSlotName = "Dogtag";

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!templateTable.Items.TryGetValue(SourceDogtagCaseTpl, out var source))
            throw new InvalidOperationException("B&A&HB Dogtag Case source template is missing; refusing fallback cloning.");

        var sourceProperties = source.Properties
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case source properties are missing; refusing fallback cloning.");
        var sourceGrids = sourceProperties.Grids?.ToArray();
        if (sourceGrids == null || sourceGrids.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case source grid boundary is missing or ambiguous; exactly one canonical grid is required.");

        var sourceGrid = sourceGrids[0];
        var sourceGridProperties = sourceGrid.Properties
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case canonical source grid properties are missing; refusing fallback cloning.");
        var sourceFilters = sourceGridProperties.Filters?.ToArray();
        if (sourceFilters == null || sourceFilters.Length == 0 || sourceFilters.Any(x => x.Filter == null || x.Filter.Count == 0))
            throw new InvalidOperationException("B&A&HB Dogtag Case source filters are empty or ambiguous; refusing to create a broadened container.");

        HashSet<MongoId> dogtagSlotFilter = PrepareDogtagSlotFilter();
        var handbookItem = templateTable.Handbook.Items.FirstOrDefault(x => x.Id == SourceDogtagCaseTpl)
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case source handbook entry is missing.");

        if (templateTable.Items.TryGetValue(DogtagCaseTpl, out var existing))
        {
            // Keep the immediate preload collision proof explicit, then perform the
            // stronger live reference-identity reproof before host publication.
            ValidateExisting(existing, source);
            RequireCanonicalRegisteredTemplate(templateTable);
            cancellationToken.ThrowIfCancellationRequested();
            CommitDogtagSlotExposure(dogtagSlotFilter, cancellationToken);
            logger.Success("B&A&HB Dogtag Case retained existing validated template; vanilla Dogtag slot filter preserved and exact container appended.");
            return Task.CompletedTask;
        }

        var copiedFilters = sourceFilters
            .Select(filter => new GridFilter
            {
                Filter = new HashSet<MongoId>(filter.Filter!),
                ExcludedFilter = filter.ExcludedFilter == null
                    ? null
                    : new HashSet<MongoId>(filter.ExcludedFilter)
            })
            .ToList();

        var details = new NewItemFromCloneDetails
        {
            NewItemName = "B&A&HB Dogtag Case",
            ItemTplToClone = SourceDogtagCaseTpl,
            ParentId = source.Parent,
            NewId = TemplateId,
            FleaPriceRoubles = 50000,
            HandbookPriceRoubles = 50000,
            HandbookParentId = handbookItem.ParentId,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new LocaleDetails
                {
                    Name = "B&A&HB Dogtag Case",
                    ShortName = "Dogtag Case",
                    Description = "A dedicated dogtag container that can be equipped in the vanilla Dogtag slot while preserving the normal personal dogtag contract."
                },
                ["ru"] = new LocaleDetails
                {
                    Name = "Жетонница B&A&HB",
                    ShortName = "Жетонница",
                    Description = "Специальный контейнер для жетонов, устанавливаемый в штатный слот Dogtag без замены обычного личного жетона."
                }
            },
            OverrideProperties = new TemplateItemProperties
            {
                BackgroundColor = sourceProperties.BackgroundColor,
                ExaminedByDefault = sourceProperties.ExaminedByDefault,
                Width = sourceProperties.Width,
                Height = sourceProperties.Height,
                StackMaxSize = sourceProperties.StackMaxSize,
                Grids =
                [
                    new Grid
                    {
                        Name = sourceGrid.Name,
                        Id = GridId,
                        Parent = TemplateId,
                        Prototype = sourceGrid.Prototype,
                        Properties = new GridProperties
                        {
                            CellsH = sourceGridProperties.CellsH,
                            CellsV = sourceGridProperties.CellsV,
                            MinCount = sourceGridProperties.MinCount,
                            MaxCount = sourceGridProperties.MaxCount,
                            MaxWeight = sourceGridProperties.MaxWeight,
                            IsSortingTable = sourceGridProperties.IsSortingTable,
                            Filters = copiedFilters
                        }
                    }
                ]
            }
        };

        // CustomItemService registration owns template/handbook/locale state that
        // cannot be proven rollback-safe here. Observe cancellation immediately
        // before that point of no return; once creation succeeds, finish the exact
        // host commit to a coherent registered product rather than leaving an
        // orphaned template because cancellation arrived during the synchronous call.
        cancellationToken.ThrowIfCancellationRequested();
        var result = customItemService.CreateItemFromClone(details);
        if (!result.Success)
            throw new InvalidOperationException($"B&A&HB Dogtag Case creation failed: {string.Join("; ", result.Errors)}");

        if (!templateTable.Items.TryGetValue(DogtagCaseTpl, out var created))
            throw new InvalidOperationException("B&A&HB Dogtag Case creation reported success but the exact template is absent; refusing Dogtag slot exposure.");
        ValidateExisting(created, source);

        // Re-resolve both canonical source and exact product after the explicit
        // post-create value proof and before exposing the product through the live
        // Dogtag host. A replaced/detached template pair fails closed here.
        RequireCanonicalRegisteredTemplate(templateTable);
        CommitDogtagSlotExposure(dogtagSlotFilter, CancellationToken.None);
        logger.Success("B&A&HB Dogtag Case created and revalidated against the canonical EFT Dogtag Case root/grid/filter contract; vanilla Dogtag slot entries preserved and exact container appended.");
        return Task.CompletedTask;
    }

    private HashSet<MongoId> PrepareDogtagSlotFilter()
    {
        if (!templateTable.Items.TryGetValue(DefaultInventoryTpl, out var inventory))
            throw new InvalidOperationException("B&A&HB default inventory template is missing for Dogtag host registration.");

        var slots = inventory.Properties?.Slots?
            .Where(x => string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (slots == null || slots.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag slot boundary is missing or ambiguous; refusing inventory mutation.");

        var groups = slots[0].Properties?.Filters?.ToArray();
        if (groups == null || groups.Length != 1 || groups[0].Filter == null || groups[0].Filter.Count == 0)
            throw new InvalidOperationException("B&A&HB Dogtag slot filter boundary is missing or ambiguous; exactly one non-empty vanilla filter group is required.");

        HashSet<MongoId> hostFilter = groups[0].Filter;
        MongoId[] vanillaEntries = hostFilter
            .Where(x => !PersistentIdentityManifest.IsOwnedTemplate(x.ToString()))
            .ToArray();
        DogtagCaseHostContract.CaptureVanillaEntries(vanillaEntries);

        foreach (MongoId accepted in hostFilter)
        {
            string templateId = accepted.ToString();
            if (PersistentIdentityManifest.IsOwnedTemplate(templateId)
                && !string.Equals(templateId, TemplateId, StringComparison.Ordinal))
                throw new InvalidOperationException("B&A&HB Dogtag slot is already contaminated by a different owned product template; refusing cross-host mutation.");
        }

        return hostFilter;
    }

    private static void CommitDogtagSlotExposure(HashSet<MongoId> filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        cancellationToken.ThrowIfCancellationRequested();

        DogtagCaseHostContract.RequirePreserved(filter);
        cancellationToken.ThrowIfCancellationRequested();

        // HashSet.Add is the mutation/ownership boundary. If another compatible
        // actor already exposed the exact case, this invocation owns no mutation
        // and must never remove that pre-existing entry during fail-closed rollback.
        bool addedHere = filter.Add(DogtagCaseTpl);
        try
        {
            // Cancellation is observed inside the same ownership boundary as
            // committed-host validation. If this invocation appended the case,
            // cancellation rolls back only that append; a pre-existing exact case
            // remains untouched because addedHere is false.
            cancellationToken.ThrowIfCancellationRequested();
            DogtagCaseHostContract.RequireCommitted(filter);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            if (addedHere)
                filter.Remove(DogtagCaseTpl);
            throw;
        }
    }

    /// <summary>
    /// Revalidates the live registered product against the live canonical EFT/SPT
    /// Dogtag Case immediately before host/trader publication. This closes startup
    /// mutation windows: another participant may not alter the B&A&HB case root
    /// footprint, stack policy, root presentation, grid geometry or filter contract
    /// and still obtain a host/trader-published corrupted product. The final
    /// reference-identity reproof also refuses a detached source/candidate pair that
    /// was replaced in TemplateTable during validation.
    /// </summary>
    public static void RequireCanonicalRegisteredTemplate(TemplateTable templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        if (!templates.Items.TryGetValue(SourceDogtagCaseTpl, out var source))
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: canonical source template is missing.");
        if (!templates.Items.TryGetValue(DogtagCaseTpl, out var candidate))
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: exact product template is missing.");
        ValidateExisting(candidate, source);

        if (!templates.Items.TryGetValue(SourceDogtagCaseTpl, out var liveSource)
            || !ReferenceEquals(liveSource, source))
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: canonical source template was replaced during validation.");
        if (!templates.Items.TryGetValue(DogtagCaseTpl, out var liveCandidate)
            || !ReferenceEquals(liveCandidate, candidate))
            throw new InvalidOperationException("B&A&HB Dogtag Case publication refused: product template was replaced during validation.");
    }

    private static void ValidateExisting(TemplateItem candidate, TemplateItem source)
    {
        if (!Equals(candidate.Parent, source.Parent))
            throw new InvalidOperationException("B&A&HB Dogtag Case ID collision: existing template uses a different parent.");

        var candidateProperties = candidate.Properties;
        var sourceProperties = source.Properties;
        if (candidateProperties == null || sourceProperties == null
            || !Equals(candidateProperties.BackgroundColor, sourceProperties.BackgroundColor)
            || candidateProperties.ExaminedByDefault != sourceProperties.ExaminedByDefault
            || candidateProperties.Width != sourceProperties.Width
            || candidateProperties.Height != sourceProperties.Height
            || candidateProperties.StackMaxSize != sourceProperties.StackMaxSize)
            throw new InvalidOperationException("B&A&HB Dogtag Case ID collision: root presentation, examined state, geometry or stack policy differs from the canonical source contract.");

        var grids = candidateProperties.Grids?.ToArray();
        var sourceGrids = sourceProperties.Grids?.ToArray();
        if (grids == null || grids.Length != 1 || sourceGrids == null || sourceGrids.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case ID collision: expected one grid.");

        var grid = grids[0];
        var sourceGrid = sourceGrids[0];
        var actual = grid.Properties;
        var expected = sourceGrid.Properties;
        if (!string.Equals(grid.Id.ToString(), GridId, StringComparison.Ordinal)
            || !string.Equals(grid.Parent?.ToString(), TemplateId, StringComparison.Ordinal)
            || !string.Equals(grid.Name, sourceGrid.Name, StringComparison.Ordinal)
            || !Equals(grid.Prototype, sourceGrid.Prototype)
            || actual == null
            || expected == null
            || actual.CellsH != expected.CellsH
            || actual.CellsV != expected.CellsV
            || actual.MinCount != expected.MinCount
            || actual.MaxCount != expected.MaxCount
            || actual.MaxWeight != expected.MaxWeight
            || actual.IsSortingTable != expected.IsSortingTable)
            throw new InvalidOperationException("B&A&HB Dogtag Case ID collision: grid identity, geometry or limits differ from the canonical source contract.");

        var actualFilters = actual.Filters?.ToArray();
        var expectedFilters = expected.Filters?.ToArray();
        if (actualFilters == null || expectedFilters == null || actualFilters.Length != expectedFilters.Length)
            throw new InvalidOperationException("B&A&HB Dogtag Case ID collision: filter-group count differs from canonical source.");

        for (int i = 0; i < expectedFilters.Length; i++)
        {
            var actualIncluded = actualFilters[i].Filter;
            var expectedIncluded = expectedFilters[i].Filter;
            var actualExcluded = actualFilters[i].ExcludedFilter;
            var expectedExcluded = expectedFilters[i].ExcludedFilter;
            if (actualIncluded == null || expectedIncluded == null || !actualIncluded.SetEquals(expectedIncluded))
                throw new InvalidOperationException("B&A&HB Dogtag Case ID collision: included filter differs from canonical source.");
            if ((actualExcluded == null) != (expectedExcluded == null)
                || (actualExcluded != null && expectedExcluded != null && !actualExcluded.SetEquals(expectedExcluded)))
                throw new InvalidOperationException("B&A&HB Dogtag Case ID collision: excluded filter differs from canonical source, including null/empty contract parity.");
        }
    }
}
