using System.Security.Claims;

namespace Autopatch.Server.Services;

/// <summary>
/// Defines a contract for validating client subscriptions to tracked collections.
/// </summary>
/// <typeparam name="T">The type of items in the collection being validated.</typeparam>
public interface ICollectionSubscriptionValidator<T> where T : class
{
    /// <summary>
    /// Validates whether a client can subscribe to a specific collection.
    /// </summary>
    /// <param name="user">The authenticated user for the current connection, or <see langword="null"/> if the connection is unauthenticated. Populated from the SignalR hub's <c>Context.User</c>.</param>
    /// <param name="authString">The authentication string provided by the client. Can be null or empty if no authentication was provided. Retained for backwards compatibility; validators that authenticate via <paramref name="user"/> may ignore it.</param>
    /// <param name="collectionKey">The key identifying the specific collection (e.g., "general", "tech-talk").</param>
    /// <returns>A task that resolves to <see langword="true"/> if the subscription is allowed; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method is called for every subscription attempt when a validator is registered for the type.
    /// The validator decides whether a null user and/or null/empty authentication string are acceptable.
    /// </remarks>
    Task<bool> ValidateSubscriptionAsync(ClaimsPrincipal? user, string? authString, string collectionKey);
}
