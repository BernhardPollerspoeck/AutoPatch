using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace Autopatch.Server.Services;

/// <summary>
/// A collection type registered with <c>AddTrackedCollection</c>.
/// </summary>
/// <param name="ItemType">The item type of the collection.</param>
/// <param name="ValidateAsync">Runs the subscription validator of the type, or allows the subscription if none is registered.</param>
internal sealed record TrackedCollectionRegistration(
    Type ItemType,
    Func<IServiceProvider, ClaimsPrincipal?, string?, string, Task<bool>> ValidateAsync)
{
    public static TrackedCollectionRegistration For<TItem>() where TItem : class
        => new(typeof(TItem), static (services, user, authString, collectionKey) =>
            services.GetService<ICollectionSubscriptionValidator<TItem>>() is { } validator
                ? validator.ValidateSubscriptionAsync(user, authString, collectionKey)
                : Task.FromResult(true));
}

/// <summary>
/// Maps the type names clients subscribe with to the registered collection types.
/// </summary>
/// <remarks>
/// Only registered types can be subscribed (fail-closed). Two registered types with the same simple name are rejected at
/// start-up, because clients address collections by the simple type name.
/// </remarks>
internal sealed class TrackedCollectionRegistry
{
    private readonly Dictionary<string, TrackedCollectionRegistration> _byName = new(StringComparer.Ordinal);

    public TrackedCollectionRegistry(IEnumerable<TrackedCollectionRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            if (_byName.TryGetValue(registration.ItemType.Name, out var existing) && existing.ItemType != registration.ItemType)
            {
                throw new InvalidOperationException(
                    $"The tracked collection types '{existing.ItemType.FullName}' and '{registration.ItemType.FullName}' have the same name. " +
                    "Clients subscribe by type name, so tracked collection types must have unique names.");
            }
            _byName[registration.ItemType.Name] = registration;
        }
    }

    public bool TryGet(string typeName, out TrackedCollectionRegistration registration)
        => _byName.TryGetValue(typeName, out registration!);
}
