using Autopatch.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ServiceDiscovery;

namespace Autopatch.Server.Extensions;

/// <summary>
/// Extension methods for configuring AutoPatch server with .NET Aspire capabilities.
/// </summary>
public static class AspireServiceCollectionExtensions
{
    /// <summary>
    /// Adds AutoPatch server services configured for .NET Aspire integration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">A delegate to configure the <see cref="AutopatchOptions"/>.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// This method configures AutoPatch server with .NET Aspire service discovery support.
    /// The server will be discoverable by clients using the service name "autopatch" by default.
    /// </remarks>
    public static IServiceCollection AddAutoPatchWithAspire(
        this IServiceCollection services,
        Action<AutopatchOptions>? configure = null)
    {
        // Add service discovery for potential future client-side discovery within server apps
        services.AddServiceDiscovery();

        // Configure AutoPatch with Aspire-friendly defaults
        services.AddAutoPatch(options =>
        {
            // Apply any custom configuration
            configure?.Invoke(options);
        });

        return services;
    }
}