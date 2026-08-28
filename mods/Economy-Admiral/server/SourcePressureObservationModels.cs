namespace SPTEconomy;

public sealed record ChannelObservationCoverage
{
    public required AcquisitionChannel Channel { get; init; }
    public required string State { get; init; }
    public required int ObservedSourceCount { get; init; }
    public string? Diagnostic { get; init; }
}

public sealed record FinalDbSourceObservation
{
    public required IReadOnlyList<AcquisitionSourceEvidence> Sources { get; init; }
    public required IReadOnlyList<AcquisitionCostPath> CostPaths { get; init; }
    public required IReadOnlyList<ChannelObservationCoverage> ChannelCoverage { get; init; }
    public required EffectiveAcquisitionGraphResult AcquisitionGraph { get; init; }
    public required double StartupMilliseconds { get; init; }
}
