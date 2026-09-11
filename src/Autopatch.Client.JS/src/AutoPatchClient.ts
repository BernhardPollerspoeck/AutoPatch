/**
 * AutoPatch Client for JavaScript/TypeScript
 * Connects to AutoPatch SignalR hub and manages real-time data synchronization
 */

import { HubConnection, HubConnectionBuilder, HubConnectionState, IRetryPolicy, LogLevel } from '@microsoft/signalr';
import { applyPatch, Operation as JsonPatchOperation } from 'fast-json-patch';
import {
  AutoPatchConfiguration,
  Operation,
  SubscriptionResult,
  ConnectionStatus,
  AutoPatchEventHandlers,
  Trackable,
  CollectionState
} from './types';

/** Delays between reconnect attempts; the last one is repeated forever. */
const RECONNECT_DELAYS_MS = [0, 2000, 10000, 30000];

/** What is needed to subscribe again after a reconnect. */
interface SubscriptionInfo {
  typeName: string;
  key?: string;
  authString?: string;
  resyncPending: boolean;
}

/**
 * AutoPatch client that manages real-time data synchronization with SignalR.
 *
 * Every batch from the server carries a sequence number. Batches that are already contained in the current state are
 * ignored; if a batch is missing or cannot be applied, the client requests the full data again. After a reconnect all
 * collections are subscribed again.
 */
export class AutoPatchClient {
  private connection: HubConnection | null = null;
  private config: AutoPatchConfiguration;
  private eventHandlers: AutoPatchEventHandlers;
  private collections: Map<string, CollectionState<any>> = new Map();
  private subscriptions: Map<string, SubscriptionInfo> = new Map();

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
    const builder = new HubConnectionBuilder()
      .withUrl(this.config.endpoint, {
        timeout: 60000, // 60 seconds timeout
        ...(this.config.accessTokenFactory ? { accessTokenFactory: this.config.accessTokenFactory } : {})
      })
      .configureLogging(LogLevel.Warning);

    if (this.config.autoReconnect) {
      const retryForever: IRetryPolicy = {
        nextRetryDelayInMilliseconds: (context) =>
          RECONNECT_DELAYS_MS[Math.min(context.previousRetryCount, RECONNECT_DELAYS_MS.length - 1)]
      };
      builder.withAutomaticReconnect(retryForever);
    }

    this.connection = builder.build();

    this.connection.onclose((error) => {
      this.notifyConnectionChanged(ConnectionStatus.Disconnected);
      if (error) {
        this.reportError(new Error(`Connection closed: ${error.message}`));
      }
    });

    this.connection.onreconnecting(() => {
      this.notifyConnectionChanged(ConnectionStatus.Reconnecting);
    });

    this.connection.onreconnected(() => {
      this.notifyConnectionChanged(ConnectionStatus.Connected);
      void this.resubscribeAll();
    });
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
      this.reportError(new Error(`Disconnect error: ${error instanceof Error ? error.message : 'Unknown error'}`));
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

    const subscriptionKey = this.getSubscriptionKey(typeName, key);
    if (this.subscriptions.has(subscriptionKey)) {
      // The collection is shared, but every call is validated with its own credentials.
      const accepted = await this.invokeSubscribe({ typeName, key, authString, resyncPending: false });
      return accepted ? { success: true } : { success: false, message: 'Subscription rejected by server' };
    }

    const methodName = this.getMethodName(subscriptionKey);
    const info: SubscriptionInfo = { typeName, key, authString, resyncPending: false };
    this.subscriptions.set(subscriptionKey, info);
    if (!this.collections.has(subscriptionKey)) {
      this.collections.set(subscriptionKey, {
        items: [],
        isInitialized: false,
        lastUpdate: new Date(),
        subscriptionKey,
        sequence: 0
      });
    }

    this.connection.on(methodName, (_: string, operations: Operation[], isInitialData: boolean, sequence?: number) => {
      this.handleDataUpdate<T>(subscriptionKey, typeName, operations, isInitialData, sequence);
    });

    try {
      const success = await this.invokeSubscribe(info);
      if (success) {
        return { success: true };
      }
      this.removeSubscription(subscriptionKey);
      return { success: false, message: 'Subscription rejected by server' };
    } catch (error) {
      this.removeSubscription(subscriptionKey);
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

    try {
      await this.connection.invoke('UnsubscribeFromType', typeName, key || null);
      this.removeSubscription(subscriptionKey);
    } catch (error) {
      this.reportError(new Error(`Unsubscribe failed: ${error instanceof Error ? error.message : 'Unknown error'}`));
    }
  }

  /**
   * Gets the current data for a subscribed type
   */
  getCollection<T extends Trackable>(typeName: string, key?: string): T[] {
    const state = this.collections.get(this.getSubscriptionKey(typeName, key));
    return state ? state.items : [];
  }

  /**
   * Checks if a collection is initialized (has received initial data)
   */
  isCollectionInitialized(typeName: string, key?: string): boolean {
    const state = this.collections.get(this.getSubscriptionKey(typeName, key));
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
   * Handles a batch from the server.
   */
  private handleDataUpdate<T extends Trackable>(
    subscriptionKey: string,
    typeName: string,
    operations: Operation[],
    isInitialData: boolean,
    sequence?: number
  ): void {
    const state = this.collections.get(subscriptionKey);
    if (!state) return;

    const hasSequence = typeof sequence === 'number';
    if (!isInitialData) {
      if (!state.isInitialized) {
        return; // Ignore until the initial set is there.
      }
      if (hasSequence && sequence! <= state.sequence) {
        return; // Already contained in the current state.
      }
      if (hasSequence && sequence! !== state.sequence + 1) {
        this.requestFullData(subscriptionKey);
        return;
      }
    }

    try {
      const cleanedOperations = (operations ?? []).map(op => {
        const cleanOp: any = { op: op.op, path: this.transformPathToCamelCase(op.path) };
        if ('value' in op && op.value !== undefined) {
          cleanOp.value = op.value;
        }
        if ('from' in op && op.from !== undefined && op.from !== null) {
          cleanOp.from = this.transformPathToCamelCase(op.from);
        }
        return cleanOp;
      });

      // Apply to a copy, so a batch that fails halfway leaves the collection untouched.
      const current = isInitialData ? [] : state.items;
      const result = applyPatch(current, cleanedOperations as readonly JsonPatchOperation[], true, false);
      const items = result.newDocument ?? current;

      state.items.length = 0;
      state.items.push(...items);
      state.lastUpdate = new Date();
      if (hasSequence) {
        state.sequence = sequence!;
      }
      if (isInitialData) {
        state.isInitialized = true;
        const info = this.subscriptions.get(subscriptionKey);
        if (info) info.resyncPending = false;
      }

      this.eventHandlers.onDataUpdated?.(typeName, operations, isInitialData);
      this.eventHandlers.onDataReceived?.(typeName, state.items, isInitialData);
    } catch (error) {
      this.reportError(new Error(`Data update failed for ${typeName}: ${error instanceof Error ? error.message : 'Unknown error'}`));
      this.requestFullData(subscriptionKey);
    }
  }

  /**
   * Ignores further batches of a collection until the server has sent the full data again.
   */
  private requestFullData(subscriptionKey: string): void {
    const state = this.collections.get(subscriptionKey);
    const info = this.subscriptions.get(subscriptionKey);
    if (!state || !info) return;

    state.isInitialized = false;
    if (info.resyncPending || this.connection?.state !== HubConnectionState.Connected) return;

    info.resyncPending = true;
    this.invokeSubscribe(info).catch((error) => {
      info.resyncPending = false;
      this.reportError(new Error(`Resync failed for ${subscriptionKey}: ${error instanceof Error ? error.message : 'Unknown error'}`));
    });
  }

  /**
   * Subscribes all collections again (used after reconnection, when the server has forgotten all groups)
   */
  private async resubscribeAll(): Promise<void> {
    const resubscriptions = Array.from(this.subscriptions.entries()).map(async ([subscriptionKey, info]) => {
      const state = this.collections.get(subscriptionKey);
      if (state) state.isInitialized = false;
      info.resyncPending = true;

      try {
        if (!(await this.invokeSubscribe(info))) {
          this.reportError(new Error(`Resubscription rejected for ${subscriptionKey}`));
        }
      } catch (error) {
        info.resyncPending = false;
        this.reportError(new Error(`Resubscription failed for ${subscriptionKey}: ${error instanceof Error ? error.message : 'Unknown error'}`));
      }
    });

    await Promise.allSettled(resubscriptions);
  }

  private invokeSubscribe(info: SubscriptionInfo): Promise<boolean> {
    return this.connection!.invoke<boolean>(
      'SubscribeToType',
      info.typeName,
      info.key || null,
      info.authString || this.config.authString || null
    );
  }

  private removeSubscription(subscriptionKey: string): void {
    this.connection?.off(this.getMethodName(subscriptionKey));
    this.subscriptions.delete(subscriptionKey);
    this.collections.delete(subscriptionKey);
  }

  private reportError(error: Error): void {
    if (this.eventHandlers.onError) {
      this.eventHandlers.onError(error);
    } else {
      console.error('[AutoPatch]', error);
    }
  }

  /**
   * Notifies event handlers of connection status changes
   */
  private notifyConnectionChanged(status: ConnectionStatus): void {
    this.eventHandlers.onConnectionChanged?.(status);
  }

  /**
   * Transforms PascalCase property paths to camelCase to match JSON serialization
   * Example: "/2/Status" -> "/2/status", "/1/CustomerName" -> "/1/customerName"
   */
  private transformPathToCamelCase(path: string): string {
    return path.replace(/\/([A-Z][a-zA-Z]*)/g, (_match, propertyName: string) => {
      const camelCase = propertyName.charAt(0).toLowerCase() + propertyName.slice(1);
      return `/${camelCase}`;
    });
  }

  /**
   * Generates subscription key for type and optional collection key
   */
  private getSubscriptionKey(typeName: string, key?: string): string {
    return key ? `${typeName}/${key}` : typeName;
  }

  private getMethodName(subscriptionKey: string): string {
    // SignalR matches client method names case-insensitively.
    return `autopatch/${subscriptionKey.toLowerCase()}`;
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
