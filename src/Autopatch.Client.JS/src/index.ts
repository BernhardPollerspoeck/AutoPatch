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

// Type definitions
export type {
  AutoPatchConfiguration,
  AutoPatchEventHandlers,
  Operation,
  SubscriptionResult,
  Trackable,
  CollectionState,
  PizzaOrder,
  DeliveryDriver
} from './types';

// Enums
export {
  ConnectionStatus,
  OrderStatus,
  DriverStatus
} from './types';

// Utilities
export { PatchUtils } from './utils/patch-utils';

// React hooks (conditionally exported)
let reactHookExports: any = {};
try {
  // Only export React hooks if React is available
  const reactHooks = require('./utils/react-hook');
  reactHookExports = {
    createReactHook: reactHooks.createReactHook,
    useAutoPatch: reactHooks.useAutoPatch,
    useAutoPatchCollection: reactHooks.useAutoPatchCollection
  };
} catch (error) {
  // React not available, skip React hook exports
}

export const { createReactHook, useAutoPatch, useAutoPatchCollection } = reactHookExports;

// Version
export const VERSION = '1.0.0';