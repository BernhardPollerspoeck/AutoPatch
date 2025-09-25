"use strict";
/**
 * AutoPatch Client for JavaScript/TypeScript
 * Connects to AutoPatch SignalR hub and manages real-time data synchronization
 */
Object.defineProperty(exports, "__esModule", { value: true });
exports.AutoPatchClient = void 0;
const signalr_1 = require("@microsoft/signalr");
const fast_json_patch_1 = require("fast-json-patch");
const types_1 = require("./types");
/**
 * AutoPatch client that manages real-time data synchronization with SignalR
 */
class AutoPatchClient {
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
        this.connection = new signalr_1.HubConnectionBuilder()
            .withUrl(this.config.endpoint)
            .withAutomaticReconnect(this.config.autoReconnect ? [0, 2000, 10000, 30000] : [])
            .configureLogging(signalr_1.LogLevel.Information)
            .build();
        // Connection state change handlers
        this.connection.onclose((error) => {
            this.notifyConnectionChanged(types_1.ConnectionStatus.Disconnected);
            if (error && this.eventHandlers.onError) {
                this.eventHandlers.onError(new Error(`Connection closed: ${error.message}`));
            }
        });
        this.connection.onreconnecting((error) => {
            this.notifyConnectionChanged(types_1.ConnectionStatus.Reconnecting);
            if (error && this.eventHandlers.onError) {
                this.eventHandlers.onError(new Error(`Reconnecting: ${error.message}`));
            }
        });
        this.connection.onreconnected((connectionId) => {
            this.notifyConnectionChanged(types_1.ConnectionStatus.Connected);
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
        if (!this.connection) {
            throw new Error('Connection not initialized');
        }
        if (this.connection.state === signalr_1.HubConnectionState.Connected) {
            return;
        }
        try {
            this.notifyConnectionChanged(types_1.ConnectionStatus.Connecting);
            await this.connection.start();
            this.notifyConnectionChanged(types_1.ConnectionStatus.Connected);
        }
        catch (error) {
            this.notifyConnectionChanged(types_1.ConnectionStatus.Disconnected);
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
            this.notifyConnectionChanged(types_1.ConnectionStatus.Disconnecting);
            await this.connection.stop();
            this.notifyConnectionChanged(types_1.ConnectionStatus.Disconnected);
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
        if (!this.connection || this.connection.state !== signalr_1.HubConnectionState.Connected) {
            throw new Error('Not connected to server');
        }
        try {
            const subscriptionKey = this.getSubscriptionKey(typeName, key);
            const methodName = `AutoPatch/${subscriptionKey}`;
            // Register handler for this specific subscription
            this.connection.on(methodName, (methodName, operations, isInitialData) => {
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
        if (!this.connection || this.connection.state !== signalr_1.HubConnectionState.Connected) {
            throw new Error('Not connected to server');
        }
        const subscriptionKey = this.getSubscriptionKey(typeName, key);
        const methodName = `AutoPatch/${subscriptionKey}`;
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
            return types_1.ConnectionStatus.Disconnected;
        switch (this.connection.state) {
            case signalr_1.HubConnectionState.Connected:
                return types_1.ConnectionStatus.Connected;
            case signalr_1.HubConnectionState.Connecting:
                return types_1.ConnectionStatus.Connecting;
            case signalr_1.HubConnectionState.Reconnecting:
                return types_1.ConnectionStatus.Reconnecting;
            case signalr_1.HubConnectionState.Disconnecting:
                return types_1.ConnectionStatus.Disconnecting;
            default:
                return types_1.ConnectionStatus.Disconnected;
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
                // Apply JSON Patch operations
                const updatedItems = (0, fast_json_patch_1.deepClone)(state.items);
                const patchResult = (0, fast_json_patch_1.applyPatch)(updatedItems, operations, false, false);
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
                }
                else {
                    throw new Error('Failed to apply some patch operations');
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
exports.AutoPatchClient = AutoPatchClient;
//# sourceMappingURL=AutoPatchClient.js.map