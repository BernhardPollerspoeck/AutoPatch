using Autopatch.Demo.Shared;
using Autopatch.Server.Services;

namespace Autopatch.Demo.Server;

/// <summary>
/// Example validator for PizzaOrder subscriptions.
/// This demonstrates how to implement subscription validation for specific collections.
/// </summary>
public class PizzaOrderValidator : ICollectionSubscriptionValidator<PizzaOrder>
{
    /// <summary>
    /// Validates whether a client can subscribe to a specific pizza order collection.
    /// </summary>
    /// <param name="authString">The authentication string provided by the client (can be null/empty).</param>
    /// <param name="collectionKey">The key identifying the specific collection (e.g., "store1", "store2").</param>
    /// <returns>True if the subscription is allowed; otherwise, false.</returns>
    public bool ValidateSubscription(string? authString, string collectionKey)
    {
        // Reject if no authentication provided
        if (string.IsNullOrEmpty(authString))
            return false;
        
        // Example validation logic:
        // - "admin" token can access any collection
        // - "store1_user" can only access "store1" collection
        // - "store2_user" can only access "store2" collection
        
        if (authString == "admin")
            return true;
            
        if (authString == "store1_user" && collectionKey == "store1")
            return true;
            
        if (authString == "store2_user" && collectionKey == "store2")
            return true;
            
        // Reject all other combinations
        return false;
    }
}
