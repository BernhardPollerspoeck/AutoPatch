# AutoPatch JavaScript/TypeScript Client

A JavaScript/TypeScript client library for the AutoPatch real-time synchronization framework. Provides seamless integration with AutoPatch SignalR hubs and automatic JSON Patch operations.

## Features

- 🔄 Real-time data synchronization via SignalR
- 📦 JSON Patch operations for efficient updates
- 🎯 TypeScript support with full type safety
- ⚛️ React hooks for easy integration
- 🔌 Simple, Promise-based API
- 🛡️ Automatic reconnection handling
- 📊 Connection status monitoring

## Installation

```bash
npm install @autopatch/client
```

## Basic Usage

### Simple Connection

```typescript
import { AutoPatchClient, ConnectionStatus } from '@autopatch/client';

const client = new AutoPatchClient({
  endpoint: 'http://localhost:5249/Autopatch'
}, {
  onConnectionChanged: (status) => {
    console.log('Connection status:', status);
  },
  onDataReceived: (typeName, data, isInitialData) => {
    console.log(`Received ${data.length} ${typeName} items`);
  }
});

await client.connect();

// Subscribe to data updates
const result = await client.subscribeToType('PizzaOrder');
if (result.success) {
  console.log('Successfully subscribed to PizzaOrder updates');
}

// Get current data
const orders = client.getCollection('PizzaOrder');
console.log('Current orders:', orders);
```

### With Authentication

```typescript
const result = await client.subscribeToType('SecureData', 'store1', 'your-auth-token');
```

### React Integration

```typescript
import React from 'react';
import { AutoPatchClient, PizzaOrder, useAutoPatch, useAutoPatchCollection } from '@autopatch/client';

function PizzaTracker() {
  const { client, isConnected, connect } = useAutoPatch({
    endpoint: 'http://localhost:5249/Autopatch',
    autoConnect: true
  });

  const { data: orders, isInitialized } = useAutoPatchCollection<PizzaOrder>(client, {
    typeName: 'PizzaOrder',
    autoSubscribe: true
  });

  if (!isConnected) return <div>Connecting...</div>;
  if (!isInitialized) return <div>Loading orders...</div>;

  return (
    <div>
      <h1>Pizza Orders ({orders.length})</h1>
      {orders.map(order => (
        <div key={order.orderId}>
          {order.customerName}: {order.status}
        </div>
      ))}
    </div>
  );
}
```

## API Reference

### AutoPatchClient

#### Constructor

```typescript
new AutoPatchClient(config, eventHandlers?)
```

**Config Options:**
- `endpoint` (string): SignalR hub endpoint URL
- `authString?` (string): Default authentication string
- `autoReconnect?` (boolean): Enable automatic reconnection (default: true)
- `reconnectDelay?` (number): Reconnection delay in milliseconds (default: 3000)
- `dispatcher?` (function): UI update dispatcher for threading

**Event Handlers:**
- `onConnectionChanged?` (status) => void
- `onError?` (error) => void
- `onDataReceived?` (typeName, data, isInitialData) => void
- `onDataUpdated?` (typeName, operations, isInitialData) => void

#### Methods

##### connect(): Promise<void>
Connects to the AutoPatch server.

##### disconnect(): Promise<void>
Disconnects from the AutoPatch server.

##### subscribeToType<T>(typeName, key?, authString?): Promise<SubscriptionResult>
Subscribes to real-time updates for a specific data type.

##### unsubscribeFromType(typeName, key?): Promise<void>
Unsubscribes from updates for a specific data type.

##### getCollection<T>(typeName, key?): T[]
Gets the current data for a subscribed type.

##### isCollectionInitialized(typeName, key?): boolean
Checks if a collection has received initial data.

##### getConnectionStatus(): ConnectionStatus
Gets the current connection status.

### React Hooks

#### useAutoPatch(config, eventHandlers?)

Returns an object with:
- `client`: AutoPatch client instance
- `connectionStatus`: Current connection status
- `isConnected`: Boolean connection status
- `connect()`: Manual connect function
- `disconnect()`: Manual disconnect function
- `error`: Last error encountered

#### useAutoPatchCollection<T>(client, config)

**Config:**
- `typeName`: The data type to subscribe to
- `key?`: Optional collection key
- `authString?`: Optional authentication
- `autoSubscribe?`: Auto-subscribe when client connects

Returns an object with:
- `data`: Array of items
- `isInitialized`: Boolean initialization status
- `isSubscribed`: Boolean subscription status
- `subscribe()`: Manual subscribe function
- `unsubscribe()`: Manual unsubscribe function
- `error`: Last error encountered
- `lastUpdate`: Timestamp of last update

## Demo Applications

### Pizza Delivery Demo

A complete React application demonstrating real-time pizza order and driver tracking:

```bash
cd examples/react-demo
npm install
npm start
```

The demo connects to the AutoPatch demo server and displays:
- 🍕 Live pizza orders with status updates
- 🚗 Delivery driver locations and assignments
- 🔗 Real-time connection status
- 🎨 Beautiful, responsive UI

### Features Demonstrated

- Real-time data synchronization
- Connection status monitoring
- Error handling and recovery
- Type-safe TypeScript integration
- Responsive React components
- JSON Patch operation handling

## Connection States

- `Disconnected`: Not connected to server
- `Connecting`: Attempting to connect
- `Connected`: Successfully connected and ready
- `Reconnecting`: Attempting to reconnect after connection loss
- `Disconnecting`: Gracefully disconnecting

## Error Handling

The client automatically handles:
- Connection failures with retry logic
- JSON Patch operation failures
- SignalR reconnection
- Authentication failures

Error information is provided through the `onError` event handler and React hook error states.

## Browser Support

- Chrome 60+
- Firefox 60+
- Safari 12+
- Edge 79+

Requires ES2020 support and WebSocket connectivity.

## License

MIT - See LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make changes with tests
4. Submit a pull request

For bugs and feature requests, please use GitHub issues.