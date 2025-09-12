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
    /// <param name="authString">The authentication string provided by the client. Can be null or empty if no authentication was provided.</param>
    /// <param name="collectionKey">The key identifying the specific collection (e.g., "general", "tech-talk").</param>
    /// <returns>True if the subscription is allowed; otherwise, false.</returns>
    /// <remarks>
    /// This method is called for every subscription attempt when a validator is registered for the type.
    /// The validator decides whether null/empty authentication strings are acceptable.
    /// </remarks>
    bool ValidateSubscription(string? authString, string collectionKey);
}
