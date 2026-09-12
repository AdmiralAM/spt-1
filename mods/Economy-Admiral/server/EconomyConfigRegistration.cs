using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SPTarkov.Server.Core.DI;

namespace SPTEconomy;

public sealed class EconomyConfigRegistration : IOnDIConstruct
{
    public static async Task OnDIConstructAsync(IServiceCollection serviceCollection, CancellationToken cancellationToken)
    {
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Economy Admiral config: unable to resolve assembly directory.");
        var config = await EconomyConfigBootstrap.LoadOrCreateAsync(Path.Combine(assemblyDirectory, "config"), cancellationToken);
        serviceCollection.AddSingleton(config);
    }
}