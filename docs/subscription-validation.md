# AutoPatch Subscription Validation

This document explains how to use the subscription validation feature in AutoPatch.

## Overview

The subscription validation feature allows you to control which clients can subscribe to specific collections based on an authentication string they provide.

## Server-Side Setup

### 1. Create a Validator

Implement the `ICollectionSubscriptionValidator<T>` interface:

```csharp
public class PizzaOrderValidator : ICollectionSubscriptionValidator<PizzaOrder>
{
    public bool ValidateSubscription(string? authString, string collectionKey)
    {
        // IMPORTANT: This method is ALWAYS called if a validator is registered,
        // regardless of whether the client provided an auth string or not.
        
        // Reject if no authentication provided
        if (string.IsNullOrEmpty(authString))
            return false;
        
        // Your validation logic here
        if (authString == "admin")
            return true;
            
        if (authString == "store1_user" && collectionKey == "store1")
            return true;
            
        return false; // Reject by default
    }
}
```

### 2. Register with Validation

Use the overload when registering your tracked collection:

```csharp
builder.Services
    .AddTrackedCollection<PizzaOrder, PizzaOrderValidator>(cfg =>
    {
        // Your configuration
    });
```

### 3. Register without Validation (Optional)

For collections that don't need validation, use the regular method:

```csharp
builder.Services
    .AddTrackedCollection<DeliveryDriver>(cfg =>
    {
        // Your configuration
    });
```

## Client-Side Usage

### Subscribe with Authentication

```csharp
// This will be validated by PizzaOrderValidator
bool success = await client.SubscribeToTypeAsync<PizzaOrder>("store1", "admin");

if (success)
{
    // Subscription successful
    var collection = client.GetTrackedCollection<PizzaOrder>("store1");
}
else
{
    // Subscription was rejected
    Console.WriteLine("Access denied");
}
```

### Subscribe without Authentication

```csharp
// If a validator exists, this will likely be rejected (depends on validator logic)
bool success = await client.SubscribeToTypeAsync<PizzaOrder>("store1");
// The validator will receive null as authString and decide what to do

// For collections without validators, this will succeed
bool success = await client.SubscribeToTypeAsync<DeliveryDriver>();
```

## Key Points

- **Security First**: If a validator is registered, it is **ALWAYS** called, even when no auth string is provided.
- **Validator Decides**: The validator itself decides whether null/empty auth strings are acceptable.
- **Per Collection Type**: Each collection type can have its own validator.
- **Collection Keys**: The validator receives both the auth string and the collection key, allowing fine-grained access control.
- **Backward Compatibility**: Existing code without auth strings continues to work, but may be rejected if validators are added.
- **Return Value**: The subscription method now returns `bool` to indicate success/failure.

## Validation Logic

The validator method receives:
- `authString`: The authentication token/string provided by the client (can be null/empty)
- `collectionKey`: The specific collection being accessed (e.g., "store1", "general", "tech-talk")

Return `true` to allow the subscription, `false` to reject it.

## Security Considerations

**Important**: Once you register a validator for a collection type, **all** subscription attempts will be validated. This prevents security bypasses where clients could skip authentication by not providing an auth string.
