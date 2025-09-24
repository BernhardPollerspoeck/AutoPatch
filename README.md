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
services.AddAutoPatch(cfg => cfg.Endpoint = "http://localhost:5249")
        .AddTrackedCollection<Order>();

// Usage
public class OrderViewModel
{
    public ObservableCollection<Order> Orders { get; private set; } = [];

    public async Task InitializeAsync()
    {
        await _client.SubscribeToTypeAsync<Order>();
        Orders = _client.GetTrackedCollection<Order>(); // Auto-updating collection
    }
}
```

## 🌟 .NET Aspire Integration

AutoPatch seamlessly integrates with .NET Aspire for service discovery, eliminating the need for hardcoded URLs.

### Aspire Server Setup

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(); // Aspire service defaults

builder.Services
    .AddAutoPatchWithAspire(cfg => cfg.DefaultThrottleInterval = TimeSpan.FromMilliseconds(500))
    .AddTrackedCollection<Order>()
    .AddSignalR();

var app = builder.Build();
app.MapDefaultEndpoints(); // Aspire endpoints
app.UseAutoPatch();
app.Run();
```

### Aspire Client Setup

```csharp
// Client with service discovery - no hardcoded URLs!
builder.AddServiceDefaults();

services.AddAutoPatchWithServiceDiscovery("autopatch") // Discover the "autopatch" service
        .AddTrackedCollection<Order>();

// Usage remains the same
await client.SubscribeToTypeAsync<Order>();
var orders = client.GetTrackedCollection<Order>();
```

### Benefits of Aspire Integration

- 🔍 **Automatic Service Discovery** - No hardcoded URLs needed
- 🌍 **Environment Agnostic** - Works in dev, test, and production
- 📊 **Built-in Observability** - Metrics, logging, and tracing
- 🏥 **Health Monitoring** - Automatic health checks
- ⚙️ **Centralized Configuration** - Manage settings through Aspire

## 📖 Advanced Features

### Multiple Collections & Authentication

```csharp
// Server: Collection per tenant
var tenantOrders = manager.GetOrCreateCollection<Order>($"tenant_{tenantId}");

// Client: Subscribe with auth
await client.SubscribeToTypeAsync<Order>("tenant_123", "auth_token");
var orders = client.GetTrackedCollection<Order>("tenant_123");

// Validator
public class OrderValidator : ICollectionSubscriptionValidator<Order>
{
    public bool ValidateSubscription(string? auth, string key) => auth == "valid_token";
}
builder.Services.AddTrackedCollection<Order, OrderValidator>();
```

### Configuration Options

```csharp
builder.Services.AddTrackedCollection<Order>(cfg =>
{
    cfg.ThrottleInterval = TimeSpan.FromMilliseconds(100);  // Update frequency
    cfg.ExcludedProperties = ["InternalData"];              // Skip properties
    cfg.ClientChangePolicy = ClientChangePolicy.Reject;    // Read-only
});
```

## 🏗️ How It Works

**Server**: `ObservableCollection<T>` changes → JsonPatch → SignalR broadcast  
**Client**: Receive patches → Apply to local `ObservableCollection<T>` → UI updates

## 🌟 Roadmap

- [ ] **Bidirectional Sync** - Client-to-server change propagation
- [ ] **Change Policies** - Auto/RequireConfirmation/Reject modes  
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
