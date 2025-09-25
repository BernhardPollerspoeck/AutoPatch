"use strict";
/**
 * React hooks for AutoPatch client integration
 * Provides easy-to-use hooks for React applications
 */
Object.defineProperty(exports, "__esModule", { value: true });
exports.useAutoPatch = useAutoPatch;
exports.useAutoPatchCollection = useAutoPatchCollection;
exports.createReactHook = createReactHook;
const react_1 = require("react");
const AutoPatchClient_1 = require("../AutoPatchClient");
const types_1 = require("../types");
/**
 * React hook for managing AutoPatch client connection
 */
function useAutoPatch(config, eventHandlers) {
    const [client, setClient] = (0, react_1.useState)(null);
    const [connectionStatus, setConnectionStatus] = (0, react_1.useState)(types_1.ConnectionStatus.Disconnected);
    const [error, setError] = (0, react_1.useState)(null);
    const clientRef = (0, react_1.useRef)(null);
    // Initialize client
    (0, react_1.useEffect)(() => {
        const newClient = new AutoPatchClient_1.AutoPatchClient(config, {
            ...eventHandlers,
            onConnectionChanged: (status) => {
                setConnectionStatus(status);
                eventHandlers?.onConnectionChanged?.(status);
            },
            onError: (err) => {
                setError(err);
                eventHandlers?.onError?.(err);
            }
        });
        setClient(newClient);
        clientRef.current = newClient;
        // Auto-connect if configured
        if (config.autoConnect !== false) {
            newClient.connect().catch(setError);
        }
        return () => {
            newClient.dispose();
        };
    }, [config.endpoint]); // Only recreate if endpoint changes
    const connect = (0, react_1.useCallback)(async () => {
        if (clientRef.current) {
            try {
                setError(null);
                await clientRef.current.connect();
            }
            catch (err) {
                setError(err instanceof Error ? err : new Error('Connection failed'));
            }
        }
    }, []);
    const disconnect = (0, react_1.useCallback)(async () => {
        if (clientRef.current) {
            try {
                await clientRef.current.disconnect();
            }
            catch (err) {
                setError(err instanceof Error ? err : new Error('Disconnect failed'));
            }
        }
    }, []);
    return {
        client,
        connectionStatus,
        connect,
        disconnect,
        isConnected: connectionStatus === types_1.ConnectionStatus.Connected,
        error
    };
}
/**
 * React hook for managing a specific data collection
 */
function useAutoPatchCollection(client, config) {
    const [data, setData] = (0, react_1.useState)([]);
    const [isInitialized, setIsInitialized] = (0, react_1.useState)(false);
    const [isSubscribed, setIsSubscribed] = (0, react_1.useState)(false);
    const [error, setError] = (0, react_1.useState)(null);
    const [lastUpdate, setLastUpdate] = (0, react_1.useState)(null);
    // Update data when client data changes
    (0, react_1.useEffect)(() => {
        if (!client)
            return;
        const interval = setInterval(() => {
            const currentData = client.getCollection(config.typeName, config.key);
            const initialized = client.isCollectionInitialized(config.typeName, config.key);
            setData(currentData);
            setIsInitialized(initialized);
            setLastUpdate(new Date());
        }, 100); // Check for updates every 100ms
        return () => clearInterval(interval);
    }, [client, config.typeName, config.key]);
    // Auto-subscribe when client connects
    (0, react_1.useEffect)(() => {
        if (!client || !config.autoSubscribe)
            return;
        const subscribe = async () => {
            try {
                const result = await client.subscribeToType(config.typeName, config.key, config.authString);
                if (result.success) {
                    setIsSubscribed(true);
                    setError(null);
                }
                else {
                    setError(new Error(result.message || 'Subscription failed'));
                }
            }
            catch (err) {
                setError(err instanceof Error ? err : new Error('Subscription error'));
            }
        };
        // Subscribe when client connects
        if (client.getConnectionStatus() === types_1.ConnectionStatus.Connected) {
            subscribe();
        }
    }, [client, config.typeName, config.key, config.authString, config.autoSubscribe]);
    const subscribe = (0, react_1.useCallback)(async () => {
        if (!client) {
            const result = { success: false, message: 'Client not available' };
            setError(new Error(result.message));
            return result;
        }
        try {
            const result = await client.subscribeToType(config.typeName, config.key, config.authString);
            if (result.success) {
                setIsSubscribed(true);
                setError(null);
            }
            else {
                setError(new Error(result.message || 'Subscription failed'));
            }
            return result;
        }
        catch (err) {
            const error = err instanceof Error ? err : new Error('Subscription error');
            setError(error);
            return { success: false, message: error.message };
        }
    }, [client, config.typeName, config.key, config.authString]);
    const unsubscribe = (0, react_1.useCallback)(async () => {
        if (!client)
            return;
        try {
            await client.unsubscribeFromType(config.typeName, config.key);
            setIsSubscribed(false);
            setData([]);
            setIsInitialized(false);
            setError(null);
        }
        catch (err) {
            setError(err instanceof Error ? err : new Error('Unsubscribe error'));
        }
    }, [client, config.typeName, config.key]);
    return {
        data,
        isInitialized,
        isSubscribed,
        subscribe,
        unsubscribe,
        error,
        lastUpdate
    };
}
/**
 * Factory function to create AutoPatch React hooks with pre-configured settings
 */
function createReactHook(defaultConfig) {
    return {
        useAutoPatch: (config, eventHandlers) => useAutoPatch({ ...defaultConfig, ...config }, eventHandlers),
        useAutoPatchCollection: (client, config) => useAutoPatchCollection(client, config)
    };
}
//# sourceMappingURL=react-hook.js.map