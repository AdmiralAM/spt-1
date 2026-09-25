using AdmiralCompatibilitySuite.Seasons;

var enabled = new[] { 0, 1, 4, 2, 5, 3 }; // STORM disabled by upstream config.
var state = new SeasonRotationState(1, 0, 0, null);
for (var raid = 1; raid <= 5; raid++)
{
    state = SeasonCyclePolicy.Advance(state, enabled, $"raid-{raid}")!;
    if (state.Season != (raid == 5 ? 1 : 0) || state.CompletedInSeason != raid % 5)
        throw new Exception($"Wrong season/count after raid {raid}: {state}");
}
if (SeasonCyclePolicy.Advance(state, enabled, "raid-5") is not null)
    throw new Exception("Duplicate end-raid notification advanced the counter.");
if (SeasonCyclePolicy.Advance(state, enabled, null) is not null)
    throw new Exception("Missing raid identity advanced the counter.");

for (var raid = 6; raid <= 30; raid++)
    state = SeasonCyclePolicy.Advance(state, enabled, $"raid-{raid}")!;
if (state.Season != 0 || state.CompletedInSeason != 0)
    throw new Exception("The cycle did not return to the first enabled season.");

var resumed = state with { CompletedInSeason = 4, LastServerId = "raid-30" };
var next = SeasonCyclePolicy.Advance(resumed, enabled, "raid-31")!;
if (next.Season != 1 || next.CompletedInSeason != 0)
    throw new Exception("A restored four-raid state did not switch after the fifth raid.");

Console.WriteLine("Season Rotator five-raid cycle contract passed.");
