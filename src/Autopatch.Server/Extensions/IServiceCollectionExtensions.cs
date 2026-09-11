using System.ComponentModel;
using Autopatch.Core;
using Autopatch.Server.Models;
using Autopatch.Server.Services;
using Autopatch.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Autopatch.Server.Extensions;
/// <summary>
/// Provides extension methods for configuring Autopatch services in the dependency injection container.
/// </summary>
public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds Autopatch services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">A delegate to configure the <see cref="AutopatchOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <remarks>
    /// This method registers the core Autopatch services including the hosted service for collection tracking.
    /// </remarks>
    public static IServiceCollection AddAutoPatch(
        this IServiceCollection services,
        Action<AutopatchOptions> configure)
    {
        services.Configure(configure);

        services.AddHostedService<AutoPatchCollectionTrackerService>();
        services.TryAddSingleton<ITrackedCollectionManager, TrackedCollectionManager>();
        services.TryAddSingleton<TrackedCollectionRegistry>();
        services.TryAddSingleton(sp => new AutoPatchHubClients(sp.GetRequiredService<IHubContext<AutoPatchHub>>(), sp));

        return services;
    }

    /// <summary>
    /// Registers a tracked observable collection type with the dependency injection container.
    /// Collections can be created at runtime with different keys using ITrackedCollectionManager.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection. Must be a reference type that implements <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An optional delegate to configure the object type configuration for the tracked collection.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <remarks>
    /// This method registers the necessary services for creating tracked collections at runtime.
    /// Use ITrackedCollectionManager.GetOrCreateCollection&lt;TItem&gt;(key) to get collections with specific keys.
    /// Clients can only subscribe to registered types. Registered types must have unique simple names.
    /// </remarks>
    public static IServiceCollection AddTrackedCollection<TItem>(
            this IServiceCollection services,
            Action<ObjectTypeConfiguration<OperationContainer<TItem>>>? configure = null)
            where TItem : class, INotifyPropertyChanged
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        // Register factory for creating trackers at runtime
        services.TryAddSingleton<ITrackedCollectionFactory<TItem>, TrackedCollectionFactory<TItem>>();
        services.AddSingleton(TrackedCollectionRegistration.For<TItem>());

        return services;
    }

    /// <summary>
    /// Registers a tracked observable collection type with subscription validation.
    /// Collections can be created at runtime with different keys using ITrackedCollectionManager.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection. Must be a reference type that implements <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TValidator">The validator type that implements <see cref="ICollectionSubscriptionValidator{TItem}"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An optional delegate to configure the object type configuration for the tracked collection.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <remarks>
    /// This method registers the necessary services for creating tracked collections at runtime with subscription validation.
    /// Use ITrackedCollectionManager.GetOrCreateCollection&lt;TItem&gt;(key) to get collections with specific keys.
    /// </remarks>
    public static IServiceCollection AddTrackedCollection<TItem, TValidator>(
            this IServiceCollection services,
            Action<ObjectTypeConfiguration<OperationContainer<TItem>>>? configure = null)
            where TItem : class, INotifyPropertyChanged
            where TValidator : class, ICollectionSubscriptionValidator<TItem>
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        // Register factory for creating trackers at runtime
        services.TryAddSingleton<ITrackedCollectionFactory<TItem>, TrackedCollectionFactory<TItem>>();
        services.AddSingleton(TrackedCollectionRegistration.For<TItem>());

        // Register the validator
        services.AddScoped<ICollectionSubscriptionValidator<TItem>, TValidator>();

        return services;
    }
}
