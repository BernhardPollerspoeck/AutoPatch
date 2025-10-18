using Autopatch.Client.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ServiceDiscovery;

namespace Autopatch.Client.Extensions;

/// <summary>
/// Extension methods for configuring AutoPatch with .NET Aspire service discovery.
/// </summary>
public static class AspireServiceCollectionExtensions
{
    /// <summary>
    /// Adds AutoPatch client services configured for .NET Aspire service discovery.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="serviceName">The name of the AutoPatch server service to discover.</param>
    /// <param name="configureClient">Optional additional client configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// This method automatically configures service discovery and sets up the AutoPatch client
    /// to discover the server through .NET Aspire's service discovery mechanism.
    /// </remarks>    
    public static IServiceCollection AddAutoPatchWithServiceDiscovery(
        this IServiceCollection services,
        string serviceName,
        Action<AutoPatchConfiguration>? configureClient = null)
    {
        // Add service discovery if not already added
        services.AddServiceDiscovery();

        // Configure AutoPatch with service discovery
        services.AddAutoPatch(config =>
        {
            config.ServiceName = serviceName;
            configureClient?.Invoke(config);
        });

        return services;
    }

    /// <summary>
    /// Adds AutoPatch client services configured for .NET Aspire service discovery.
    /// This overload uses the default service name "autopatch".
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureClient">Optional additional client configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddAutoPatchWithServiceDiscovery(
        this IServiceCollection services,
        Action<AutoPatchConfiguration>? configureClient = null)
    {
        return services.AddAutoPatchWithServiceDiscovery("autopatch", configureClient);
    }
}
