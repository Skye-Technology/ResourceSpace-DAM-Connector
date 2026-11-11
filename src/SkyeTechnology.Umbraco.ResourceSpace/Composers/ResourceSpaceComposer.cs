using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using SkyeTechnology.Umbraco.ResourceSpace.Abstractions;
using SkyeTechnology.Umbraco.ResourceSpace.Configuration;
using SkyeTechnology.Umbraco.ResourceSpace.ResourceSpace;

namespace SkyeTechnology.Umbraco.ResourceSpace.Composers;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ResourceSpace DAM connector, typed HTTP client, caching, and options binding.
    /// Consumers can swap <see cref="IDamConnector"/> before or after calling this method to plug in another DAM.
    /// </summary>
    public static IServiceCollection AddResourceSpaceDam(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services
            .AddOptions<ResourceSpaceOptions>()
            .BindConfiguration(ResourceSpaceOptions.SectionName);

        services.AddHttpClient<ResourceSpaceClient>((sp, httpClient) =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<ResourceSpaceOptions>>().CurrentValue;
            httpClient.Timeout = opts.HttpTimeout > TimeSpan.Zero ? opts.HttpTimeout : TimeSpan.FromSeconds(10);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SkyeTechnology-Umbraco-ResourceSpace/1.0");
        });

        services.TryAddSingleton<IDamConnector, ResourceSpaceDamConnector>();

        return services;
    }
}

/// <summary>
/// Auto-discovers and registers ResourceSpace DAM services when the package is referenced by an Umbraco host.
/// </summary>
public sealed class ResourceSpaceComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddResourceSpaceDam();
    }
}
