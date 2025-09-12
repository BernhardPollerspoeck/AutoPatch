using System.Collections.ObjectModel;
using Autopatch.Client.Models;
using Autopatch.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Autopatch.Client.Extensions;

/// <summary>
/// Extension methods for configuring AutoPatch services in the dependency injection container.
/// </summary>
public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds AutoPatch client services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">A delegate to configure AutoPatch options.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddAutoPatch(this IServiceCollection services, Action<AutoPatchConfiguration> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IAutoPatchClient, AutoPatchClient>();
        services.AddHostedService<AutopatchConnectionManager>();

        return services;
    }

    /// <summary>
    /// Adds a tracked observable collection for the specified item type to the dependency injection container.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="services">The service collection to add the tracked collection to.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// This method creates a new observable collection instance for each registration.
    /// Multiple collections of the same type are supported through the AutoPatch client's key-based subscription system.
    /// </remarks>
    public static IServiceCollection AddTrackedCollection<TItem>(this IServiceCollection services)
    {
        services.AddTransient<ObservableCollection<TItem>>();

        return services;
    }
}
