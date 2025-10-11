/**
 * AutoPatch Client for JavaScript/TypeScript
 * Connects to AutoPatch SignalR hub and manages real-time data synchronization
 */
import { AutoPatchConfiguration, SubscriptionResult, ConnectionStatus, AutoPatchEventHandlers, Trackable } from './types';
/**
 * AutoPatch client that manages real-time data synchronization with SignalR
 */
export declare class AutoPatchClient {
    private connection;
    private config;
    private eventHandlers;
    private collections;
    private subscriptions;
    constructor(config: AutoPatchConfiguration, eventHandlers?: AutoPatchEventHandlers);
    /**
     * Sets up the SignalR connection with proper event handlers
     */
    private setupConnection;
    /**
     * Sets up handlers for receiving data updates from the server
     */
    private setupDataHandlers;
    /**
     * Connects to the AutoPatch server
     */
    connect(): Promise<void>;
    /**
     * Disconnects from the AutoPatch server
     */
    disconnect(): Promise<void>;
    /**
     * Subscribes to real-time updates for a specific data type
     */
    subscribeToType<T extends Trackable>(typeName: string, key?: string, authString?: string): Promise<SubscriptionResult>;
    /**
     * Unsubscribes from real-time updates for a specific data type
     */
    unsubscribeFromType(typeName: string, key?: string): Promise<void>;
    /**
     * Gets the current data for a subscribed type
     */
    getCollection<T extends Trackable>(typeName: string, key?: string): T[];
    /**
     * Checks if a collection is initialized (has received initial data)
     */
    isCollectionInitialized(typeName: string, key?: string): boolean;
    /**
     * Gets current connection status
     */
    getConnectionStatus(): ConnectionStatus;
    /**
     * Handles incoming data updates from the server
     */
    private handleDataUpdate;
    /**
     * Re-subscribes to all previously subscribed types (used after reconnection)
     */
    private resubscribeAll;
    /**
     * Notifies event handlers of connection status changes
     */
    private notifyConnectionChanged;
    /**
     * Transforms PascalCase property paths to camelCase to match JSON serialization
     * Example: "/2/Status" -> "/2/status", "/1/CustomerName" -> "/1/customerName"
     */
    private transformPathToCamelCase;
    /**
     * Generates subscription key for type and optional collection key
     */
    private getSubscriptionKey;
    /**
     * Disposes the client and cleans up resources
     */
    dispose(): Promise<void>;
}
//# sourceMappingURL=AutoPatchClient.d.ts.map