using System.Collections.ObjectModel;
using System.ComponentModel;
using Autopatch.Core;
using Autopatch.Server.Models;
using Autopatch.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        return services;
    }

    /// <summary>
    /// Registers a tracked observable collection of the specified item type with the dependency injection container.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection. Must be a reference type that implements <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An optional delegate to configure the object type configuration for the tracked collection.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <remarks>
    /// This method registers all necessary services for tracking an observable collection including:
    /// <list type="bullet">
    /// <item><description>The collection tracker service</description></item>
    /// <item><description>Object tracker interfaces</description></item>
    /// <item><description>The observable collection itself</description></item>
    /// <item><description>A bulk flush queue for operations</description></item>
    /// </list>
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

        services.AddSingleton<ObservableCollectionTracker<TItem>>();
        services.AddSingleton<IObjectTracker<ObservableCollection<TItem>, TItem>>(sp => sp.GetRequiredService<ObservableCollectionTracker<TItem>>());
        services.AddSingleton<IObjectTracker>(sp => sp.GetRequiredService<ObservableCollectionTracker<TItem>>());
        services.AddSingleton(sp => sp.GetRequiredService<IObjectTracker<ObservableCollection<TItem>, TItem>>().TrackedCollection);
        services.AddSingleton<BulkFlushQueue<OperationContainer<TItem>>>();

        return services;
    }
}
