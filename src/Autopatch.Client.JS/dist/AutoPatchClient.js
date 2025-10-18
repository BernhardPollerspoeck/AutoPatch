/**
 * AutoPatch Client for JavaScript/TypeScript
 * Connects to AutoPatch SignalR hub and manages real-time data synchronization
 */
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { applyPatch } from 'fast-json-patch';
import { ConnectionStatus } from './types';
/**
 * AutoPatch client that manages real-time data synchronization with SignalR
 */
export class AutoPatchClient {
    constructor(config, eventHandlers = {}) {
        this.connection = null;
        this.collections = new Map();
        this.subscriptions = new Set();
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
    setupConnection() {
        this.connection = new HubConnectionBuilder()
            .withUrl(this.config.endpoint, {
            timeout: 60000 // 60 seconds timeout
        })
            .withAutomaticReconnect(this.config.autoReconnect ? [0, 2000, 10000, 30000] : [])
            .configureLogging(LogLevel.Information)
            .build();
        // Connection state change handlers
        this.connection.onclose((error) => {
            console.log('[AutoPatch] Connection closed:', error);
            this.notifyConnectionChanged(ConnectionStatus.Disconnected);
            if (error && this.eventHandlers.onError) {
                this.eventHandlers.onError(new Error(`Connection closed: ${error.message}`));
            }
        });
        this.connection.onreconnecting((error) => {
            console.log('[AutoPatch] Reconnecting:', error);
            this.notifyConnectionChanged(ConnectionStatus.Reconnecting);
            if (error && this.eventHandlers.onError) {
                this.eventHandlers.onError(new Error(`Reconnecting: ${error.message}`));
            }
        });
        this.connection.onreconnected((connectionId) => {
            console.log('[AutoPatch] Reconnected with ID:', connectionId);
            this.notifyConnectionChanged(ConnectionStatus.Connected);
            this.resubscribeAll();
        });
        // Register handlers for data updates  
        this.setupDataHandlers();
    }
    /**
     * Sets up handlers for receiving data updates from the server
     */
    setupDataHandlers() {
        if (!this.connection)
            return;
        // The server sends updates via dynamic method names like "AutoPatch/PizzaOrder"
        // We'll register a generic handler that can handle any AutoPatch/* method
    }
    /**
     * Connects to the AutoPatch server
     */
    async connect() {
        console.log('[AutoPatch] Starting connection process...');
        if (!this.connection) {
            throw new Error('Connection not initialized');
        }
        console.log('[AutoPatch] Current connection state:', this.connection.state);
        if (this.connection.state === HubConnectionState.Connected) {
            console.log('[AutoPatch] Already connected, skipping...');
            return;
        }
        try {
            console.log('[AutoPatch] Setting status to Connecting...');
            this.notifyConnectionChanged(ConnectionStatus.Connecting);
            console.log('[AutoPatch] Calling connection.start()...');
            await this.connection.start();
            console.log('[AutoPatch] Connection started successfully! State:', this.connection.state);
            this.notifyConnectionChanged(ConnectionStatus.Connected);
        }
        catch (error) {
            console.error('[AutoPatch] Connection failed:', error);
            this.notifyConnectionChanged(ConnectionStatus.Disconnected);
            throw new Error(`Failed to connect: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }
    /**
     * Disconnects from the AutoPatch server
     */
    async disconnect() {
        if (!this.connection)
            return;
        try {
            this.notifyConnectionChanged(ConnectionStatus.Disconnecting);
            await this.connection.stop();
            this.notifyConnectionChanged(ConnectionStatus.Disconnected);
        }
        catch (error) {
            if (this.eventHandlers.onError) {
                this.eventHandlers.onError(new Error(`Disconnect error: ${error instanceof Error ? error.message : 'Unknown error'}`));
            }
        }
    }
    /**
     * Subscribes to real-time updates for a specific data type
     */
    async subscribeToType(typeName, key, authString) {
        if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
            throw new Error('Not connected to server');
        }
        try {
            const subscriptionKey = this.getSubscriptionKey(typeName, key);
            const methodName = `autopatch/${subscriptionKey.toLowerCase()}`;
            // Check if already subscribed
            if (this.subscriptions.has(subscriptionKey)) {
                console.log(`[AutoPatch] Already subscribed to ${subscriptionKey}`);
                return { success: true };
            }
            console.log(`[AutoPatch] Registering handler for method: ${methodName}`);
            // Register handler for this specific subscription
            this.connection.on(methodName, (receivedMethodName, operations, isInitialData) => {
                this.handleDataUpdate(subscriptionKey, typeName, operations, isInitialData);
            });
            // Call server method to subscribe
            const success = await this.connection.invoke('SubscribeToType', typeName, key || null, authString || this.config.authString || null);
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
            }
            else {
                // Remove handler if subscription failed
                this.connection.off(methodName);
                return { success: false, message: 'Subscription rejected by server' };
            }
        }
        catch (error) {
            return {
                success: false,
                message: `Subscription failed: ${error instanceof Error ? error.message : 'Unknown error'}`
            };
        }
    }
    /**
     * Unsubscribes from real-time updates for a specific data type
     */
    async unsubscribeFromType(typeName, key) {
        if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
            throw new Error('Not connected to server');
        }
        const subscriptionKey = this.getSubscriptionKey(typeName, key);
        const methodName = `autopatch/${subscriptionKey.toLowerCase()}`;
        try {
            await this.connection.invoke('UnsubscribeFromType', typeName, key || null);
            this.connection.off(methodName);
            this.subscriptions.delete(subscriptionKey);
            this.collections.delete(subscriptionKey);
        }
        catch (error) {
            if (this.eventHandlers.onError) {
                this.eventHandlers.onError(new Error(`Unsubscribe failed: ${error instanceof Error ? error.message : 'Unknown error'}`));
            }
        }
    }
    /**
     * Gets the current data for a subscribed type
     */
    getCollection(typeName, key) {
        const subscriptionKey = this.getSubscriptionKey(typeName, key);
        const state = this.collections.get(subscriptionKey);
        return state ? state.items : [];
    }
    /**
     * Checks if a collection is initialized (has received initial data)
     */
    isCollectionInitialized(typeName, key) {
        const subscriptionKey = this.getSubscriptionKey(typeName, key);
        const state = this.collections.get(subscriptionKey);
        return state ? state.isInitialized : false;
    }
    /**
     * Gets current connection status
     */
    getConnectionStatus() {
        if (!this.connection)
            return ConnectionStatus.Disconnected;
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
    handleDataUpdate(subscriptionKey, typeName, operations, isInitialData) {
        const state = this.collections.get(subscriptionKey);
        if (!state)
            return;
        try {
            if (operations && operations.length > 0) {
                // Clean operations and transform PascalCase paths to camelCase
                const cleanedOperations = operations.map(op => {
                    const cleanOp = {
                        op: op.op,
                        path: this.transformPathToCamelCase(op.path)
                    };
                    // Add value if it exists (for add, replace, test operations)
                    if ('value' in op && op.value !== undefined) {
                        cleanOp.value = op.value;
                    }
                    // Add from if it exists and is not null (for move, copy operations)
                    if ('from' in op && op.from !== undefined && op.from !== null) {
                        cleanOp.from = this.transformPathToCamelCase(op.from);
                    }
                    return cleanOp;
                }).filter(op => op.op && op.path !== undefined);
                // Apply JSON Patch operations directly to the singleton array
                try {
                    const patchResult = applyPatch(state.items, cleanedOperations, true, false);
                    // Check if we got a newDocument in the result
                    if (patchResult.newDocument) {
                        // Replace the array contents with the new document
                        state.items.length = 0; // Clear array
                        state.items.push(...patchResult.newDocument); // Add new items
                    }
                    if (patchResult.length === 0 || !patchResult.some(r => r.test === false)) {
                        // All patches applied successfully
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
                    }
                    else {
                        console.error(`[AutoPatch] Some patch operations failed:`, patchResult.filter(r => r.test === false));
                        throw new Error('Failed to apply some patch operations');
                    }
                }
                catch (patchError) {
                    console.error(`[AutoPatch] Patch application failed for ${typeName}:`, patchError);
                    console.error(`[AutoPatch] Operations that failed:`, cleanedOperations);
                    console.error(`[AutoPatch] Current array length:`, state.items.length);
                    console.error(`[AutoPatch] Failed operation:`, patchError.operation);
                    // Log the target object to debug property names
                    if (patchError.operation?.path) {
                        const pathParts = patchError.operation.path.split('/');
                        if (pathParts.length >= 2) {
                            const index = parseInt(pathParts[1]);
                            if (!isNaN(index) && state.items[index]) {
                                console.error(`[AutoPatch] Target object at index ${index}:`, state.items[index]);
                                console.error(`[AutoPatch] Object keys:`, Object.keys(state.items[index]));
                            }
                        }
                    }
                    throw patchError;
                }
            }
        }
        catch (error) {
            if (this.eventHandlers.onError) {
                this.eventHandlers.onError(new Error(`Data update failed: ${error instanceof Error ? error.message : 'Unknown error'}`));
            }
        }
    }
    /**
     * Re-subscribes to all previously subscribed types (used after reconnection)
     */
    async resubscribeAll() {
        const subscriptionPromises = Array.from(this.subscriptions).map(async (subscriptionKey) => {
            const [typeName, key] = subscriptionKey.includes('/')
                ? subscriptionKey.split('/', 2)
                : [subscriptionKey, undefined];
            try {
                await this.subscribeToType(typeName, key);
            }
            catch (error) {
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
    notifyConnectionChanged(status) {
        if (this.eventHandlers.onConnectionChanged) {
            this.eventHandlers.onConnectionChanged(status);
        }
    }
    /**
     * Transforms PascalCase property paths to camelCase to match JSON serialization
     * Example: "/2/Status" -> "/2/status", "/1/CustomerName" -> "/1/customerName"
     */
    transformPathToCamelCase(path) {
        return path.replace(/\/([A-Z][a-zA-Z]*)/g, (match, propertyName) => {
            // Convert first letter to lowercase
            const camelCase = propertyName.charAt(0).toLowerCase() + propertyName.slice(1);
            return `/${camelCase}`;
        });
    }
    /**
     * Generates subscription key for type and optional collection key
     */
    getSubscriptionKey(typeName, key) {
        return key ? `${typeName}/${key}` : typeName;
    }
    /**
     * Disposes the client and cleans up resources
     */
    async dispose() {
        await this.disconnect();
        this.collections.clear();
        this.subscriptions.clear();
        this.connection = null;
    }
}
//# sourceMappingURL=AutoPatchClient.js.map