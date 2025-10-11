/**
 * AutoPatch JavaScript/TypeScript Client
 *
 * A client library for connecting to the AutoPatch real-time synchronization framework.
 * Provides seamless integration with SignalR hubs and automatic JSON Patch operations.
 *
 * @example
 * ```typescript
 * import { AutoPatchClient, ConnectionStatus } from '@autopatch/client';
 *
 * const client = new AutoPatchClient({
 *   endpoint: 'http://localhost:5249/Autopatch'
 * }, {
 *   onConnectionChanged: (status) => console.log('Connection:', status),
 *   onDataReceived: (typeName, data) => console.log('Data:', typeName, data)
 * });
 *
 * await client.connect();
 * await client.subscribeToType('PizzaOrder');
 * ```
 */
// Main client exports
export { AutoPatchClient } from './AutoPatchClient';
// Enums
export { ConnectionStatus, OrderStatus, DriverStatus } from './types';
// Utilities
export { PatchUtils } from './utils/patch-utils';
// React hooks
export { createReactHook, useAutoPatch, useAutoPatchCollection } from './utils/react-hook';
// Version
export const VERSION = '1.0.0';
//# sourceMappingURL=index.js.map