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

// React hooks
export {
  createReactHook,
  useAutoPatch,
  useAutoPatchCollection,
  type UseAutoPatchConfig,
  type UseAutoPatchReturn,
  type UseAutoPatchCollectionConfig,
  type UseAutoPatchCollectionReturn
} from './utils/react-hook';

// Version
export const VERSION = '1.0.0';