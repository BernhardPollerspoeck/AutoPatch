/**
 * AutoPatch Client for JavaScript/TypeScript
 * Connects to AutoPatch SignalR hub and manages real-time data synchronization
 */

import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { applyPatch, deepClone, Operation as JsonPatchOperation } from 'fast-json-patch';
import { 
  AutoPatchConfiguration, 
  Operation, 
  SubscriptionResult, 
  ConnectionStatus, 
  AutoPatchEventHandlers,
  Trackable,
  CollectionState
} from './types';

/**
 * AutoPatch client that manages real-time data synchronization with SignalR
 */
export class AutoPatchClient {
  private connection: HubConnection | null = null;
  private config: AutoPatchConfiguration;
  private eventHandlers: AutoPatchEventHandlers;
  private collections: Map<string, CollectionState<any>> = new Map();
  private subscriptions: Set<string> = new Set();

  constructor(config: AutoPatchConfiguration, eventHandlers: AutoPatchEventHandlers = {}) {
    this.config = { 
      autoReconnect: true, 
      reconnectDelay: 3000, 
      ...config 
    };
    this.eventHandlers = eventHandlers;
    this.setupConnection();
  }

  /**
   * Sets up the SignalR connection with proper event handlers
   */
  private setupConnection(): void {
    this.connection = new HubConnectionBuilder()
      .withUrl(this.config.endpoint)
      .withAutomaticReconnect(this.config.autoReconnect ? [0, 2000, 10000, 30000] : [])
      .configureLogging(LogLevel.Information)
      .build();

    // Connection state change handlers
    this.connection.onclose((error) => {
      this.notifyConnectionChanged(ConnectionStatus.Disconnected);
      if (error && this.eventHandlers.onError) {
        this.eventHandlers.onError(new Error(`Connection closed: ${error.message}`));
      }
    });

    this.connection.onreconnecting((error) => {
      this.notifyConnectionChanged(ConnectionStatus.Reconnecting);
      if (error && this.eventHandlers.onError) {
        this.eventHandlers.onError(new Error(`Reconnecting: ${error.message}`));
      }
    });

    this.connection.onreconnected((connectionId) => {
      this.notifyConnectionChanged(ConnectionStatus.Connected);
      this.resubscribeAll();
    });

    // Register handlers for data updates  
    this.setupDataHandlers();
  }

  /**
   * Sets up handlers for receiving data updates from the server
   */
  private setupDataHandlers(): void {
    if (!this.connection) return;

    // The server sends updates via dynamic method names like "AutoPatch/PizzaOrder"
    // We'll register a generic handler that can handle any AutoPatch/* method
  }

  /**
   * Connects to the AutoPatch server
   */
  async connect(): Promise<void> {
    if (!this.connection) {
      throw new Error('Connection not initialized');
    }

    if (this.connection.state === HubConnectionState.Connected) {
      return;
    }

    try {
      this.notifyConnectionChanged(ConnectionStatus.Connecting);
      await this.connection.start();
      this.notifyConnectionChanged(ConnectionStatus.Connected);
    } catch (error) {
      this.notifyConnectionChanged(ConnectionStatus.Disconnected);
      throw new Error(`Failed to connect: ${error instanceof Error ? error.message : 'Unknown error'}`);
    }
  }

  /**
   * Disconnects from the AutoPatch server
   */
  async disconnect(): Promise<void> {
    if (!this.connection) return;

    try {
      this.notifyConnectionChanged(ConnectionStatus.Disconnecting);
      await this.connection.stop();
      this.notifyConnectionChanged(ConnectionStatus.Disconnected);
    } catch (error) {
      if (this.eventHandlers.onError) {
        this.eventHandlers.onError(new Error(`Disconnect error: ${error instanceof Error ? error.message : 'Unknown error'}`));
      }
    }
  }

  /**
   * Subscribes to real-time updates for a specific data type
   */
  async subscribeToType<T extends Trackable>(
    typeName: string, 
    key?: string, 
    authString?: string
  ): Promise<SubscriptionResult> {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      throw new Error('Not connected to server');
    }

    try {
      const subscriptionKey = this.getSubscriptionKey(typeName, key);
      const methodName = `AutoPatch/${subscriptionKey}`;

      // Register handler for this specific subscription
      this.connection.on(methodName, (methodName: string, operations: Operation[], isInitialData: boolean) => {
        this.handleDataUpdate<T>(subscriptionKey, typeName, operations, isInitialData);
      });

      // Call server method to subscribe
      const success = await this.connection.invoke<boolean>(
        'SubscribeToType', 
        typeName, 
        key || null, 
        authString || this.config.authString || null
      );

      if (success) {
        this.subscriptions.add(subscriptionKey);
        
        // Initialize collection state if not exists
        if (!this.collections.has(subscriptionKey)) {
          this.collections.set(subscriptionKey, {
            items: [],
            isInitialized: false,
            lastUpdate: new Date(),
            subscriptionKey
          });
        }

        return { success: true };
      } else {
        // Remove handler if subscription failed
        this.connection.off(methodName);
        return { success: false, message: 'Subscription rejected by server' };
      }
    } catch (error) {
      return { 
        success: false, 
        message: `Subscription failed: ${error instanceof Error ? error.message : 'Unknown error'}` 
      };
    }
  }

  /**
   * Unsubscribes from real-time updates for a specific data type
   */
  async unsubscribeFromType(typeName: string, key?: string): Promise<void> {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      throw new Error('Not connected to server');
    }

    const subscriptionKey = this.getSubscriptionKey(typeName, key);
    const methodName = `AutoPatch/${subscriptionKey}`;

    try {
      await this.connection.invoke('UnsubscribeFromType', typeName, key || null);
      this.connection.off(methodName);
      this.subscriptions.delete(subscriptionKey);
      this.collections.delete(subscriptionKey);
    } catch (error) {
      if (this.eventHandlers.onError) {
        this.eventHandlers.onError(new Error(`Unsubscribe failed: ${error instanceof Error ? error.message : 'Unknown error'}`));
      }
    }
  }

  /**
   * Gets the current data for a subscribed type
   */
  getCollection<T extends Trackable>(typeName: string, key?: string): T[] {
    const subscriptionKey = this.getSubscriptionKey(typeName, key);
    const state = this.collections.get(subscriptionKey);
    return state ? state.items : [];
  }

  /**
   * Checks if a collection is initialized (has received initial data)
   */
  isCollectionInitialized(typeName: string, key?: string): boolean {
    const subscriptionKey = this.getSubscriptionKey(typeName, key);
    const state = this.collections.get(subscriptionKey);
    return state ? state.isInitialized : false;
  }

  /**
   * Gets current connection status
   */
  getConnectionStatus(): ConnectionStatus {
    if (!this.connection) return ConnectionStatus.Disconnected;
    
    switch (this.connection.state) {
      case HubConnectionState.Connected:
        return ConnectionStatus.Connected;
      case HubConnectionState.Connecting:
        return ConnectionStatus.Connecting;
      case HubConnectionState.Reconnecting:
        return ConnectionStatus.Reconnecting;
      case HubConnectionState.Disconnecting:
        return ConnectionStatus.Disconnecting;
      default:
        return ConnectionStatus.Disconnected;
    }
  }

  /**
   * Handles incoming data updates from the server
   */
  private handleDataUpdate<T extends Trackable>(
    subscriptionKey: string, 
    typeName: string, 
    operations: Operation[], 
    isInitialData: boolean
  ): void {
    const state = this.collections.get(subscriptionKey);
    if (!state) return;

    try {
      if (operations && operations.length > 0) {
        // Clean operations by removing non-standard properties and ensuring valid structure
        const cleanedOperations = operations.map(op => {
          const cleanOp: any = {
            op: op.op,
            path: op.path
          };
          
          // Add value if it exists (for add, replace, test operations)
          if ('value' in op && op.value !== undefined) {
            cleanOp.value = op.value;
          }
          
          // Add from if it exists (for move, copy operations)
          if ('from' in op && op.from !== undefined) {
            cleanOp.from = op.from;
          }
          
          return cleanOp;
        }).filter(op => op.op && op.path !== undefined);

        // Apply JSON Patch operations
        const updatedItems = deepClone(state.items);
        const patchResult = applyPatch(updatedItems, cleanedOperations as readonly JsonPatchOperation[], false, false);
        
        if (patchResult.length === 0 || !patchResult.some(r => r.test === false)) {
          // All patches applied successfully
          state.items = updatedItems;
          state.lastUpdate = new Date();
          
          if (isInitialData) {
            state.isInitialized = true;
          }

          // Notify event handlers
          if (this.eventHandlers.onDataUpdated) {
            this.eventHandlers.onDataUpdated(typeName, operations, isInitialData);
          }

          if (this.eventHandlers.onDataReceived) {
            this.eventHandlers.onDataReceived(typeName, state.items, isInitialData);
          }

          // Apply dispatcher if configured
          if (this.config.dispatcher) {
            this.config.dispatcher(() => {
              // Dispatcher callback - useful for React state updates
            });
          }
        } else {
          throw new Error('Failed to apply some patch operations');
        }
      }
    } catch (error) {
      if (this.eventHandlers.onError) {
        this.eventHandlers.onError(new Error(`Data update failed: ${error instanceof Error ? error.message : 'Unknown error'}`));
      }
    }
  }

  /**
   * Re-subscribes to all previously subscribed types (used after reconnection)
   */
  private async resubscribeAll(): Promise<void> {
    const subscriptionPromises = Array.from(this.subscriptions).map(async (subscriptionKey) => {
      const [typeName, key] = subscriptionKey.includes('/') 
        ? subscriptionKey.split('/', 2) 
        : [subscriptionKey, undefined];
      
      try {
        await this.subscribeToType(typeName, key);
      } catch (error) {
        if (this.eventHandlers.onError) {
          this.eventHandlers.onError(new Error(`Resubscription failed for ${subscriptionKey}: ${error instanceof Error ? error.message : 'Unknown error'}`));
        }
      }
    });

    await Promise.allSettled(subscriptionPromises);
  }

  /**
   * Notifies event handlers of connection status changes
   */
  private notifyConnectionChanged(status: ConnectionStatus): void {
    if (this.eventHandlers.onConnectionChanged) {
      this.eventHandlers.onConnectionChanged(status);
    }
  }

  /**
   * Generates subscription key for type and optional collection key
   */
  private getSubscriptionKey(typeName: string, key?: string): string {
    return key ? `${typeName}/${key}` : typeName;
  }

  /**
   * Disposes the client and cleans up resources
   */
  async dispose(): Promise<void> {
    await this.disconnect();
    this.collections.clear();
    this.subscriptions.clear(); 
    this.connection = null;
  }
}