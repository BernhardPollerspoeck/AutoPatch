# 🩹 AutoPatch Framework

[![NuGet Server](https://img.shields.io/nuget/v/AutoPatch.Server.svg)](https://www.nuget.org/packages/AutoPatch.Server/)
[![NuGet Client](https://img.shields.io/nuget/v/AutoPatch.Client.svg)](https://www.nuget.org/packages/AutoPatch.Client/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/BernhardPollerspoeck/autopatch/nuget.yml?branch=main)](https://github.com/BernhardPollerspoeck/autopatch/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Real-time object synchronization between server and client using SignalR and JsonPatch**

AutoPatch Framework enables automatic, transparent real-time synchronization of objects between server and client. Once configured, objects are kept in sync automatically without additional code - just subscribe and watch your objects update in real-time.

## ✨ Features

- 🔄 **Automatic Real-time Sync** - Objects stay synchronized without manual intervention
- 🚀 **Performance Optimized** - Intelligent throttling and batching system
- 📱 **UI Integration** - Seamless data binding via INotifyPropertyChanged
- 🛡️ **Type Safety** - Strongly typed API with compile-time validation
- 🎯 **Minimal API** - Just Subscribe/Unsubscribe - everything else is automatic
- 📦 **JsonPatch Based** - Efficient delta updates, only changes are transmitted
- 🔌 **SignalR Powered** - Built on proven real-time communication infrastructure
- 🔑 **Multiple Collections** - Support for multiple keyed collections of the same type
- 🔐 **Subscription Validation** - Per-collection authentication and authorization control

## 🎯 Use Cases

Live Dashboards • Real-time Tracking • Status Monitoring • Live Feeds • IoT Applications • Multi-tenant Systems

## 🚀 Quick Start

### Installation

```bash
dotnet add package AutoPatch.Server  # Server
dotnet add package AutoPatch.Client  # Client
```

### Server Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAutoPatch(cfg => cfg.DefaultThrottleInterval = TimeSpan.FromMilliseconds(500))
    .AddTrackedCollection<Order>()
    .AddSignalR();

var app = builder.Build();
app.UseAutoPatch();
app.Run();

// Use tracked collections
public class OrderService
{
    private readonly ObservableCollection<Order> _orders;

    public OrderService(ITrackedCollectionManager manager)
    {
        _orders = manager.GetOrCreateCollection<Order>();
    }

    public void ProcessOrder(Order order)
    {
        _orders.Add(order);              // → Auto-sync to clients
        order.Status = "Processing";     // → Auto-sync property changes
    }
}
```

### Client Setup

```csharp
// App setup
services.AddAutoPatch(cfg => cfg.Endpoint = "http://localhost:5249/autopatch")
        .AddTrackedCollection<Order>()
        .AddAutoPatchHostedConnection(); // optional: connect in the background when the host starts

// Usage
public class OrderViewModel
{
    public ObservableCollection<Order> Orders { get; private set; } = [];

    public async Task InitializeAsync()
    {
        await _client.ConnectAsync();                   // no-op if already connected
        await _client.SubscribeToTypeAsync<Order>();
        Orders = _client.GetTrackedCollection<Order>(); // Auto-updating collection
    }
}
```

The client reconnects automatically and subscribes again after a reconnect. Blazor WebAssembly does not run hosted services, so
call `ConnectAsync()` yourself there.
```

## 📖 Advanced Features

### Multiple Collections & Authentication

```csharp
// Server: Collection per tenant
var tenantOrders = manager.GetOrCreateCollection<Order>($"tenant_{tenantId}");

// Client: Subscribe with auth
await client.SubscribeToTypeAsync<Order>("tenant_123", "auth_token");
var orders = client.GetTrackedCollection<Order>("tenant_123");

// Validator (async; receives the authenticated ClaimsPrincipal + the legacy auth string)
public class OrderValidator : ICollectionSubscriptionValidator<Order>
{
    public Task<bool> ValidateSubscriptionAsync(ClaimsPrincipal? user, string? auth, string key)
        => Task.FromResult(auth == "valid_token");
}
builder.Services.AddTrackedCollection<Order, OrderValidator>();

// Optional: require an authenticated user on the hub endpoint (opt-in per app)
app.UseAutoPatch().RequireAuthorization();

// Client: send a bearer token (JWT) so the hub sees an authenticated Context.User
services.AddAutoPatch(cfg =>
{
    cfg.Endpoint = "https://server/autopatch";
    cfg.AccessTokenProvider = () => tokenService.GetAccessTokenAsync();
});
```

Only collection types registered with `AddTrackedCollection` can be subscribed; unknown type names are rejected.

### Configuration Options

```csharp
builder.Services.AddTrackedCollection<Order>(cfg =>
{
    cfg.ThrottleInterval = TimeSpan.FromMilliseconds(100);  // Update frequency
    cfg.ExcludedProperties = ["InternalData"];              // Never sent to clients
    cfg.FlushMode = FlushMode.Manual;                       // Only send on FlushAsync
});

// e.g. exactly one batch per simulation tick
await manager.FlushAsync<Order>(zoneKey);
```

| `FlushMode` | A batch is sent |
|---|---|
| `Timed` (default) | at the latest one throttle interval after the first change, or earlier at `MaxBatchSize` |
| `MaxBatchSize` | at `MaxBatchSize` or on `FlushAsync` |
| `Manual` | only on `FlushAsync` |

### Own Hub

To use a single connection for your own hub methods and AutoPatch, derive your hub from `AutoPatchHub` and map it instead of
calling `UseAutoPatch()`:

```csharp
public class GameHub(ITrackedCollectionManager manager, IServiceProvider services) : AutoPatchHub(manager, services)
{
    public Task Move(int x, int y) => ...;
}

app.MapHub<GameHub>("/game");
```

## 🏗️ How It Works

**Server**: `ObservableCollection<T>` changes → JSON Patch operations (insert, remove, replace, move, property changes) → ordered batches with sequence numbers → SignalR broadcast  
**Client**: Receive batches → apply each batch completely or not at all → UI updates. A missing batch or a batch that does not fit makes the client fetch the full data again.

The wire protocol is described in [spec.md](spec.md).

## 🌟 Roadmap

- [ ] **Bidirectional Sync** - Client-to-server change propagation
- [ ] **Change Policies** - accept, confirm or reject client changes  
- [ ] **Filtering** - Subscription filters and conditional updates
- [ ] **Offline Support** - Sync on reconnect

## 🆘 Support & Community

Got questions? We're here to help!

🐛 **[Report Issues](https://github.com/BernhardPollerspoeck/AutoPatch/issues)** - Found a bug or have a feature request?  
💬 **[Join Discussions](https://github.com/BernhardPollerspoeck/AutoPatch/discussions)** - Ask questions, share ideas, or showcase your projects  
📧 **[Direct Contact](mailto:bernhard@pollerspoeck.at)** - Need enterprise support or consulting?  
⭐ **[Star the Project](https://github.com/BernhardPollerspoeck/AutoPatch)** - Show your support and stay updated!

---

**Made with ❤️ for real-time applications**
