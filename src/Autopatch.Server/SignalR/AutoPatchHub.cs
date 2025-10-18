using Autopatch.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace Autopatch.Server.SignalR;

/// <summary>
/// Provides a SignalR hub for managing real-time object patch synchronization between clients and tracked collections.
/// </summary>
/// <remarks>
/// This hub allows clients to subscribe to specific object types and receive real-time updates when changes occur
/// in the tracked collections. It leverages SignalR groups to manage subscriptions efficiently.
/// </remarks>
/// <param name="collectionManager">The collection manager that handles dynamic creation and retrieval of tracked collections.</param>
/// <param name="serviceProvider">Service provider for resolving validators.</param>
public class AutoPatchHub(ITrackedCollectionManager collectionManager, IServiceProvider serviceProvider) : Hub
{

    /// <summary>
    /// Subscribes the current connection to receive updates for objects of the specified type.
    /// </summary>
    /// <param name="typeName">The name of the type to subscribe to for receiving updates.</param>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <param name="authString">Optional authentication string for subscription validation.</param>
    /// <returns>A task that returns true if subscription was successful, false if rejected by validation.</returns>
    /// <remarks>
    /// When a client subscribes to a type, they are added to a SignalR group named "AutoPatch/{typeName}" or "AutoPatch/{typeName}/{key}".
    /// If a tracker exists for the specified type and key, the full current state of the data is immediately sent to the client.
    /// If a validator is registered for the type, it will always be called regardless of whether authString is provided.
    /// </remarks>
    public async Task<bool> SubscribeToType(string typeName, string? key = null, string? authString = null)
    {
        // If a validator exists, always validate (let the validator decide if null/empty auth is acceptable)
        if (!ValidateSubscription(typeName, key ?? string.Empty, authString))
        {
            return false;
        }

        var subscriptionKey = GetSubscriptionKey(typeName, key);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"AutoPatch/{subscriptionKey}");

        var tracker = collectionManager.GetAllTrackers().FirstOrDefault(t => t.GetSubscriptionKey() == subscriptionKey);
        tracker?.SendFullData(Context.ConnectionId);

        return true;
    }

    /// <summary>
    /// Validates a subscription request using the registered validator for the type.
    /// </summary>
    /// <param name="typeName">The name of the type being subscribed to.</param>
    /// <param name="collectionKey">The collection key.</param>
    /// <param name="authString">The authentication string to validate (can be null).</param>
    /// <returns>True if validation passes or no validator is registered; false if validation fails.</returns>
    private bool ValidateSubscription(string typeName, string collectionKey, string? authString)
    {
        // Try to resolve validator using reflection
        var validatorType = typeof(ICollectionSubscriptionValidator<>);

        // Find the type in loaded assemblies
        var itemType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == typeName);

        if (itemType == null)
            return true; // Type not found, allow subscription (no validation possible)

        var genericValidatorType = validatorType.MakeGenericType(itemType);
        var validator = serviceProvider.GetService(genericValidatorType);

        if (validator == null)
            return true; // No validator registered, allow subscription

        // Validator exists - ALWAYS call it, let it decide if null/empty auth is acceptable
        var method = genericValidatorType.GetMethod("ValidateSubscription");
        var result = method?.Invoke(validator, [authString ?? string.Empty, collectionKey]);

        return result is bool boolResult && boolResult;
    }

    /// <summary>
    /// Unsubscribes the current connection from receiving updates for objects of the specified type.
    /// </summary>
    /// <param name="typeName">The name of the type to unsubscribe from.</param>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <returns>A task that represents the asynchronous unsubscription operation.</returns>
    /// <remarks>
    /// This method removes the client from the SignalR group "AutoPatch/{typeName}" or "AutoPatch/{typeName}/{key}", stopping them from
    /// receiving further updates for objects of the specified type and key.
    /// </remarks>
    public Task UnsubscribeFromType(string typeName, string? key = null)
    {
        var subscriptionKey = GetSubscriptionKey(typeName, key);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"AutoPatch/{subscriptionKey}");
    }

    /// <summary>
    /// Gets the subscription key for a type and optional key parameter.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    /// <param name="key">Optional key to identify a specific collection.</param>
    /// <returns>The subscription key in format "TypeName" or "TypeName/Key".</returns>
    private static string GetSubscriptionKey(string typeName, string? key)
    {
        return string.IsNullOrEmpty(key) ? typeName : $"{typeName}/{key}";
    }

    //public Task<ClientChangeResult> SubmitClientChange(string typeName, Operation[] operations)
    //{

    //}
}
