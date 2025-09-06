using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using Autopatch.Core;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autopatch.Server;
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddAutoPatch(
        this IServiceCollection services,
        Action<AutopatchOptions> configure)
    {
        services.Configure(configure);

        services.AddHostedService<AutoPatchCollectionTrackerService>();

        return services;
    }

    public static IServiceCollection AddTrackedCollection<TItem>(
            this IServiceCollection services,
            Action<ObjectTypeConfiguration<ObservableCollectionTracker<TItem>>>? configure = null)
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
