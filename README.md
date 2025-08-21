# 🩹 AutoPatch Framework

[![NuGet Server](https://img.shields.io/nuget/v/AutoPatch.Server.svg)](https://www.nuget.org/packages/AutoPatch.Server/)
[![NuGet Client](https://img.shields.io/nuget/v/AutoPatch.Client.svg)](https://www.nuget.org/packages/AutoPatch.Client/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/yourorg/autopatch/ci.yml?branch=main)](https://github.com/yourorg/autopatch/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Real-time object synchronization between server and client using SignalR and JsonPatch**

AutoPatch Framework enables automatic, transparent real-time synchronization of objects between server and client. Once configured, objects are kept in sync automatically without additional code - just subscribe and watch your objects update in real-time.

## ✨ Features

- 🔄 **Automatic Real-time Sync** - Objects stay synchronized without manual intervention
- 🚀 **Performance Optimized** - Intelligent throttling and batching system
- 📱 **UI Integration** - Seamless data binding via INotifyPropertyChanged
- 🔀 **Bidirectional Sync** - Optional client-to-server change propagation
- 🛡️ **Type Safety** - Strongly typed API with compile-time validation
- 🎯 **Minimal API** - Just Subscribe/Unsubscribe - everything else is automatic
- 📦 **JsonPatch Based** - Efficient delta updates, only changes are transmitted
- 🔌 **SignalR Powered** - Built on proven real-time communication infrastructure

## 🎯 Use Cases

- **Live Dashboards** - Sensor data, alarm systems, monitoring
- **Real-time Tracking** - People, vehicles, devices, assets
- **Status Monitoring** - System health, process states, notifications
- **Live Feeds** - Events, alerts, collaborative editing
- **IoT Applications** - Device states, telemetry, control systems

## 🚀 Quick Start

### Installation

```bash
# Server
dotnet add package AutoPatch.Server

# Client  
dotnet add package AutoPatch.Client
```

### Server Setup

```csharp
// Program.cs or Startup.cs
services.AddSignalR()
    .AddAutoPatch();

services.AddAutoPatch(options => 
{
    options.DefaultThrottleInterval = TimeSpan.FromMilliseconds(100);
    options.MaxBatchSize = 50;
})
.AddObjectType<SensorData>(config => 
{
    config.KeyProperty = x => x.Id;
    config.ThrottleInterval = TimeSpan.FromMilliseconds(50);
});

// In your controller or service
public class SensorService
{
    private readonly IAutoPatchService _autoPatch;
    
    public SensorService(IAutoPatchService autoPatch)
    {
        _autoPatch = autoPatch;
    }
    
    public void UpdateSensor(SensorData sensor)
    {
        // Update your data store
        await _repository.UpdateAsync(sensor);
        
        // Notify all connected clients - that's it!
        _autoPatch.NotifyChanged(sensor);
    }
}
```

### Client Setup

```csharp
// Registration
services.AddAutoPatch()
    .AddObjectType<SensorData>(config => 
    {
        config.KeyProperty = x => x.Id;
    });

// Usage
public class SensorViewModel
{
    private readonly IAutoPatchClient _client;
    public ObservableCollection<SensorData> Sensors { get; } = new();
    
    public async Task StartAsync()
    {
        await _client.StartAsync();
        
        // Subscribe and get initial data + live updates
        var result = await _client.SubscribeAsync<SensorData>(Sensors);
        
        // Sensors collection now contains all current data
        // and will automatically update when server changes occur
    }
}
```

### That's it! 🎉

Your `Sensors` collection will now automatically stay in sync with the server. No manual update code needed.

## 📖 Documentation

### Server Configuration

#### Object Type Registration

```csharp
services.AddAutoPatch()
    .AddObjectType<SensorData>(config => 
    {
        config.KeyProperty = x => x.Id;                    // Required: Unique identifier
        config.ExcludeProperties = new[] { "InternalId" }; // Optional: Properties to ignore
        config.ClientChangePolicy = ClientChangePolicy.Auto; // Client edit permissions
        config.ThrottleInterval = TimeSpan.FromMilliseconds(50); // Update frequency
    });
```

#### Client Change Policies

- **`Auto`** - Client changes are immediately applied and broadcast
- **`RequireConfirmation`** - Server validates client changes before applying
- **`Reject`** - Read-only mode, client changes are rejected

#### Throttling System

AutoPatch includes an intelligent throttling system that batches high-frequency updates:

```csharp
// High-frequency updates are automatically batched
0ms:  sensor.Temperature = 25    // → Queued
10ms: sensor.Humidity = 80       // → Queued  
30ms: sensor.Status = "Alert"    // → Queued
50ms: Timer expires → All changes sent as single batch
```

#### Usage Examples

```csharp
// Single operations
_autoPatch.NotifyChanged(sensor);
_autoPatch.NotifyDeleted<SensorData>(sensorId);
_autoPatch.NotifyAdded(newSensor);

// Bulk operations
_autoPatch.NotifyChanged(sensors);        // IEnumerable<T>
_autoPatch.NotifyDeleted<SensorData>(ids); // IEnumerable<TKey>

// Batch operations (mixed)
_autoPatch.NotifyBatch(batch => 
{
    batch.Changed(sensors);
    batch.Deleted<SensorData>(sensorIds);
    batch.Added(newSensors);
});
```

### Client Configuration

#### Connection Management

```csharp
IAutoPatchClient client = serviceProvider.GetService<IAutoPatchClient>();

// Manual connection management
await client.StartAsync(cancellationToken);
await client.StopAsync(cancellationToken);

// Or use hosted service (not available in .NET MAUI)
services.AddAutoPatch()
    .AddHostedService(); // Starts automatically with the app
```

#### Subscription with Policy Information

```csharp
var sensors = new ObservableCollection<SensorData>();
var result = await client.SubscribeAsync<SensorData>(sensors);

// Adapt UI based on server policy
if (result.CanEdit)
{
    editButton.IsEnabled = true;
}
else
{
    editButton.IsEnabled = false;
    statusLabel.Text = "Read-only mode";
}

// Or check specific policy
switch (result.ClientChangePolicy)
{
    case ClientChangePolicy.Auto:
        ShowInstantEditingUI();
        break;
    case ClientChangePolicy.RequireConfirmation:
        ShowConfirmationEditingUI();
        break;
    case ClientChangePolicy.Reject:
        ShowReadOnlyUI();
        break;
}
```

#### Error Handling

```csharp
client.OnError += (exception) => HandleError(exception);
client.OnConnectionLost += () => ShowOfflineMode();
client.OnReconnected += () => 
{
    HideOfflineMode();
    // All subscriptions are automatically reactivated
};
```

### Bidirectional Sync (Optional)

Enable client-to-server synchronization with automatic change tracking:

```csharp
services.AddAutoPatch()
    .AddObjectType<SensorData>(config => 
    {
        config.KeyProperty = x => x.Id;
        config.ChangeTracking = ChangeTrackingMode.AutoCommit; // or ManualCommit
    });
```

**Change Tracking Modes:**
- **`Disabled`** - No client change tracking (default)
- **`ManualCommit`** - Automatic tracking, manual `CommitChanges()` required
- **`AutoCommit`** - Automatic tracking + immediate commit on changes

## 🏗️ Architecture

### How It Works

1. **Server-driven Updates**: Server is the single source of truth
2. **Automatic Patching**: JsonPatch updates local objects transparently  
3. **UI Integration**: INotifyPropertyChanged triggers automatic UI updates
4. **Intelligent Throttling**: Batch-queue system optimizes performance
5. **Bidirectional Sync**: Optional client changes flow back to server

### Data Flow

```
Server Change → Queue → Throttle → JsonPatch → SignalR → Client → Apply → UI Update
     ↓             ↓        ↓          ↓         ↓        ↓       ↓      ↓
NotifyChanged → Batch → Timer → Serialize → Broadcast → Patch → Object → PropertyChanged
```

### Performance Features

- **Bandwidth Efficient**: Only deltas transmitted via JsonPatch
- **Batched Updates**: High-frequency changes are automatically batched
- **Configurable Throttling**: Per-type throttle intervals
- **SignalR Scaling**: Leverages SignalR's proven infrastructure
- **Memory Optimized**: Intelligent queue management prevents memory issues

## 🔧 Advanced Configuration

### Custom Change Handlers

```csharp
public class SensorChangeHandler : IChangeHandler<SensorData>
{
    public async Task<bool> ValidateAsync(SensorData item, ChangeContext context)
    {
        // Custom validation logic
        return item.Temperature >= -50 && item.Temperature <= 100;
    }
    
    public async Task ApplyAsync(SensorData item, ChangeContext context)
    {
        // Custom application logic
        await _repository.SaveAsync(item);
    }
}

// Register handler
services.AddAutoPatch()
    .AddChangeHandler<SensorData, SensorChangeHandler>();
```

### Per-Type Throttling

```csharp
services.AddAutoPatch()
    .AddObjectType<FastSensor>(config => 
    {
        config.ThrottleInterval = TimeSpan.FromMilliseconds(50);  // Fast updates
    })
    .AddObjectType<SlowSensor>(config =>
    {
        config.ThrottleInterval = TimeSpan.FromMilliseconds(500); // Slower updates  
    })
    .AddObjectType<CriticalAlert>(config =>
    {
        config.ThrottleInterval = TimeSpan.Zero; // No throttling - immediate
    });
```

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)  
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

- 📖 [Documentation](https://github.com/yourorg/autopatch/wiki)
- 🐛 [Issue Tracker](https://github.com/yourorg/autopatch/issues)
- 💬 [Discussions](https://github.com/yourorg/autopatch/discussions)
- 📧 [Email Support](mailto:support@yourorg.com)

## 🌟 Roadmap

- [ ] Filtering support for subscriptions
- [ ] Conditional updates
- [ ] Offline support with sync on reconnect
- [ ] Custom serialization options
- [ ] Conflict resolution strategies  
- [ ] Priority-based throttling
- [ ] Adaptive throttling based on network conditions

---

**Made with ❤️ for real-time applications**
