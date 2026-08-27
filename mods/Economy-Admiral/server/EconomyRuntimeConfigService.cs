using SPTarkov.DI.Annotations;

namespace SPTEconomy;

[Injectable]
public sealed class EconomyRuntimeConfigService(EconomyConfig config)
{
    public Task<EconomyConfig> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(config);
    }
}
