/**
 * AutoPatch Client Types
 * TypeScript interfaces mirroring the .NET AutoPatch models
 */

/**
 * Configuration options for the AutoPatch client
 */
export interface AutoPatchConfiguration {
  /** The endpoint URL for the AutoPatch server (e.g., "http://localhost:5249/Autopatch") */
  endpoint: string;
  /** Optional dispatcher function for handling UI updates (e.g., React state updates) */
  dispatcher?: (callback: () => void) => void;
  /** Optional authentication string for server validation */
  authString?: string;
  /** Enable automatic reconnection attempts */
  autoReconnect?: boolean;
  /** Reconnection delay in milliseconds */
  reconnectDelay?: number;
}

/**
 * Re-export Operation type from fast-json-patch for compatibility
 */
export { Operation } from 'fast-json-patch';

/**
 * Import Operation for use in interfaces
 */
import { Operation } from 'fast-json-patch';

/**
 * Result of a subscription attempt
 */
export interface SubscriptionResult {
  success: boolean;
  message?: string;
}

/**
 * Connection status for the AutoPatch client
 */
export enum ConnectionStatus {
  Disconnected = 'Disconnected',
  Connecting = 'Connecting', 
  Connected = 'Connected',
  Reconnecting = 'Reconnecting',
  Disconnecting = 'Disconnecting'
}

/**
 * Event handlers interface for AutoPatch client events
 */
export interface AutoPatchEventHandlers {
  onConnectionChanged?: (status: ConnectionStatus) => void;
  onError?: (error: Error) => void;
  onDataReceived?: <T>(typeName: string, data: T[], isInitialData: boolean) => void;
  onDataUpdated?: <T>(typeName: string, operations: Operation[], isInitialData: boolean) => void;
}

/**
 * Base interface for trackable objects
 * Objects should have a unique identifier for patch operations
 */
export interface Trackable {
  [key: string]: any;
}

/**
 * Collection state management
 */
export interface CollectionState<T extends Trackable> {
  items: T[];
  isInitialized: boolean;
  lastUpdate: Date;
  subscriptionKey: string;
}

/**
 * Pizza Order model (demo)
 */
export interface PizzaOrder extends Trackable {
  orderId: string;
  customerName: string;
  items: string[];
  status: OrderStatus;
  orderTime: string; // ISO date string
  estimatedDelivery?: string; // ISO date string
  assignedDriverId?: string;
  assignedDriverName?: string;
}

/**
 * Order status enumeration (demo)
 */
export enum OrderStatus {
  Received = 'Received',
  Preparing = 'Preparing', 
  Baking = 'Baking',
  Ready = 'Ready',
  OutForDelivery = 'OutForDelivery',
  Delivered = 'Delivered'
}

/**
 * Delivery Driver model (demo)
 */
export interface DeliveryDriver extends Trackable {
  driverId: string;
  name: string;
  status: DriverStatus;
  x: number;
  y: number;
  deliverySpeed: number;
  assignedOrders: string[];
}

/**
 * Driver status enumeration (demo) 
 */
export enum DriverStatus {
  Available = 'Available',
  Assigned = 'Assigned',
  Delivering = 'Delivering', 
  Returning = 'Returning',
  Offline = 'Offline'
}