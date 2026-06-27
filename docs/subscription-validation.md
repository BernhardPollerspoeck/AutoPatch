# AutoPatch Subscription Validation

This document explains how to use the subscription validation feature in AutoPatch.

## Overview

The subscription validation feature allows you to control which clients can subscribe to specific collections. The validator receives both the authenticated `ClaimsPrincipal` of the connection (from the SignalR `Context.User`) and the legacy authentication string the client provides, so you can authenticate via real ASP.NET Core authentication, via the opaque auth string, or both.

## Server-Side Setup

### 1. Create a Validator

Implement the `ICollectionSubscriptionValidator<T>` interface:

```csharp
using System.Security.Claims;

public class PizzaOrderValidator : ICollectionSubscriptionValidator<PizzaOrder>
{
    public Task<bool> ValidateSubscriptionAsync(
        ClaimsPrincipal? user, string? authString, string collectionKey)
    {
        // IMPORTANT: This method is ALWAYS called if a validator is registered,
        // regardless of whether the client provided an auth string or not.

        // Option A: authenticate against the connection's ClaimsPrincipal
        // if (user?.Identity?.IsAuthenticated == true) { ... read tenant claim ... }

        // Option B (shown here): authenticate against the opaque auth string
        // Reject if no authentication provided
        if (string.IsNullOrEmpty(authString))
            return Task.FromResult(false);

        // Your validation logic here
        var allowed =
            authString == "admin"
            || (authString == "store1_user" && collectionKey == "store1");

        return Task.FromResult(allowed); // Reject by default
    }
}
```

> The method is asynchronous, so you can `await` a database lookup, token introspection
> endpoint, or authorization service inside the validator.

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

### 4. Require Authentication on the Hub (Optional)

The validator always receives `Context.User`, but the AutoPatch hub itself is **not**
marked `[Authorize]` — this keeps unauthenticated scenarios (and the auth-string-only
model) working. If you want ASP.NET Core to reject unauthenticated connections before
they ever reach a validator, opt in per application when mapping the hub:

```csharp
app.UseAutoPatch().RequireAuthorization();
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
- **Collection Keys**: The validator receives the `ClaimsPrincipal`, the auth string and the collection key, allowing fine-grained access control.
- **Backward Compatibility**: Existing code without auth strings continues to work, but may be rejected if validators are added. The `authString` parameter is retained so existing AutoPatch consumers keep compiling against the concept; new consumers can authenticate purely via `ClaimsPrincipal`.
- **Return Value**: The subscription method returns `Task<bool>` so validation can perform asynchronous work (DB / token introspection).

## Validation Logic

The validator method receives:
- `user`: The authenticated `ClaimsPrincipal` of the connection (from `Context.User`), or `null` when unauthenticated
- `authString`: The authentication token/string provided by the client (can be null/empty)
- `collectionKey`: The specific collection being accessed (e.g., "store1", "general", "tech-talk")

Return a `Task<bool>` resolving to `true` to allow the subscription, `false` to reject it.

## Security Considerations

**Important**: Once you register a validator for a collection type, **all** subscription attempts will be validated. This prevents security bypasses where clients could skip authentication by not providing an auth string.
