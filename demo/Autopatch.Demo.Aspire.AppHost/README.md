# AutoPatch .NET Aspire Demo

This demo showcases how to use AutoPatch Framework with .NET Aspire service discovery.

## What's Demonstrated

- **Service Discovery**: Client automatically discovers the AutoPatch server without hardcoded URLs
- **Aspire Integration**: Both server and client are configured for .NET Aspire
- **Zero Configuration**: No endpoint URLs needed in production or development

## Running the Demo

1. Ensure you have .NET 9.0 SDK installed
2. Navigate to this directory
3. Run the Aspire AppHost:
   ```bash
   dotnet run
   ```
4. Open the Aspire dashboard (URL shown in console output)
5. Watch both applications start and connect automatically

## Key Features

### Server (Autopatch.Demo.Server)
- Uses `AddAutoPatchWithAspire()` for Aspire integration
- Automatically registers with service discovery as "autopatch"
- Exposes telemetry and health endpoints

### Client (Autopatch.Demo.Aspire.Client)
- Uses `AddAutoPatchWithServiceDiscovery("autopatch")` to find server
- No hardcoded URLs - everything resolved through Aspire
- Same real-time functionality as traditional AutoPatch clients

## Benefits of Aspire Integration

1. **No Hardcoded URLs**: Services discover each other automatically
2. **Environment Agnostic**: Works in development, testing, and production
3. **Observability**: Built-in metrics, logging, and tracing
4. **Health Checks**: Automatic health monitoring
5. **Configuration**: Centralized configuration management

## Traditional vs Aspire Configuration

### Traditional AutoPatch Client
```csharp
services.AddAutoPatch(cfg => cfg.Endpoint = "http://localhost:5249");
```

### Aspire AutoPatch Client
```csharp
services.AddAutoPatchWithServiceDiscovery("autopatch");
```

The Aspire version eliminates the need for environment-specific configuration!