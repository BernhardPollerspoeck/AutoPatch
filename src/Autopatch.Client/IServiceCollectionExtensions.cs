using Microsoft.Extensions.DependencyInjection;

namespace Autopatch.Client;
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddAutoPatch(this IServiceCollection services, bool withConnectionManagement = false)
    {



        return services;
    }

    public static IServiceCollection AddObjectType<T>(this IServiceCollection services, Action<ObjectTypeConfiguration<T>> configure)
        where T : class
    {
        var config = new ObjectTypeConfiguration<T>();
        configure(config);





        return services;
    }
}

public class ObjectTypeConfiguration<T> where T : class
{
    public Func<T, object> KeySelector { get; set; } = null!;
    public ChangeTrackingMode ChangeTracking { get; set; } = ChangeTrackingMode.Auto;
}

public enum ChangeTrackingMode
{
    Auto,
    ManualCommit,
    AutoCommit,
}