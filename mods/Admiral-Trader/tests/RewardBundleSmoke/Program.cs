using AdmiralTrader.Server;

static RewardBundleCatalogItem Item(string id, string role, int value, string? family = null, string source = "spt") => new()
{
    TemplateId = id,
    Source = source,
    Roles = [role],
    Tier = "common",
    ValueRub = value,
    CompatibilityFamily = family
};

RewardBundlePolicy policy = new()
{
    SchemaVersion = 1,
    Enabled = true,
    Catalog =
    [
        Item("000000000000000000000001", "weapon", 20000, "nine-mm"),
        Item("000000000000000000000002", "weapon", 22000, "nine-mm"),
        Item("000000000000000000000003", "magazine", 3000, "nine-mm"),
        Item("000000000000000000000004", "ammunition", 6000, "nine-mm"),
        Item("000000000000000000000005", "medical", 5000),
        Item("000000000000000000000006", "medical", 7000, source: "optional-mod"),
        Item("000000000000000000000007", "magazine", 3000, "wrong-family")
    ],
    Bundles =
    [
        new RewardBundleSpec
        {
            Id = "assault-common",
            Theme = "assault",
            Tier = "common",
            Seed = "admiral-test-seed",
            MinValueRub = 30000,
            MaxValueRub = 40000,
            Slots =
            [
                new RewardBundleSlot { Role = "weapon", Required = true },
                new RewardBundleSlot { Role = "magazine", Required = true },
                new RewardBundleSlot { Role = "ammunition", Required = true },
                new RewardBundleSlot { Role = "medical", Required = false, ChancePercent = 100 }
            ]
        }
    ]
};

HashSet<string> coreAvailable = policy.Catalog
    .Where(x => x.Source == "spt")
    .Select(x => x.TemplateId)
    .ToHashSet(StringComparer.Ordinal);
GeneratedRewardBundle first = RewardBundleEngine.Generate(policy, policy.Bundles[0], coreAvailable);
GeneratedRewardBundle second = RewardBundleEngine.Generate(policy, policy.Bundles[0], coreAvailable);

Require(first == second || first.Items.SequenceEqual(second.Items), "same seed must produce the same bundle");
Require(first.Items.All(x => x.Source == "spt"), "missing optional templates must be omitted");
Require(first.Items.Single(x => x.Role == "magazine").CompatibilityFamily == first.Items.Single(x => x.Role == "weapon").CompatibilityFamily,
    "magazine must match the selected weapon family");
Require(first.Items.Single(x => x.Role == "ammunition").CompatibilityFamily == first.Items.Single(x => x.Role == "weapon").CompatibilityFamily,
    "ammunition must match the selected weapon family");
Require(first.TotalValueRub is >= 30000 and <= 40000, "bundle must stay inside its value envelope");

HashSet<string> incomplete = coreAvailable.Where(x => x != "000000000000000000000004").ToHashSet(StringComparer.Ordinal);
try
{
    RewardBundleEngine.Generate(policy, policy.Bundles[0], incomplete);
    throw new Exception("missing required ammunition should fail generation");
}
catch (RewardBundleException ex) when (ex.Stage == "generation")
{
}

List<string> publicationTarget = ["native-reward"];
try
{
    RewardBundlePublication.Execute(
        "assault-common",
        apply: () => publicationTarget.Add("staged-bundle"),
        verify: () => false,
        rollback: () => publicationTarget.Remove("staged-bundle"));
    throw new Exception("failed publication should throw");
}
catch (RewardBundleException ex) when (ex.Stage == "publication")
{
}
Require(publicationTarget.SequenceEqual(["native-reward"]), "failed publication must restore the prior reward definition");

Console.WriteLine("Reward bundle deterministic generation, optional fallback, compatibility and recoverable publication: PASS");

static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
