/**
 * React hooks for AutoPatch client integration
 * Provides easy-to-use hooks for React applications
 */
import { useState, useEffect, useCallback, useRef } from 'react';
import { AutoPatchClient } from '../AutoPatchClient';
import { ConnectionStatus } from '../types';
/**
 * React hook for managing AutoPatch client connection
 */
export function useAutoPatch(config, eventHandlers) {
    const [client, setClient] = useState(null);
    const [connectionStatus, setConnectionStatus] = useState(ConnectionStatus.Disconnected);
    const [error, setError] = useState(null);
    const clientRef = useRef(null);
    // Initialize client
    useEffect(() => {
        const newClient = new AutoPatchClient(config, {
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
    const connect = useCallback(async () => {
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
    const disconnect = useCallback(async () => {
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
        isConnected: connectionStatus === ConnectionStatus.Connected,
        error
    };
}
/**
 * React hook for managing a specific data collection
 */
export function useAutoPatchCollection(client, config) {
    const [, forceUpdate] = useState({});
    const [isInitialized, setIsInitialized] = useState(false);
    const [isSubscribed, setIsSubscribed] = useState(false);
    const [error, setError] = useState(null);
    const [lastUpdate, setLastUpdate] = useState(null);
    // Get data directly from singleton - no local state copy
    const data = client ? client.getCollection(config.typeName, config.key) : [];
    // Update initialization status when client data changes
    useEffect(() => {
        if (!client)
            return;
        const interval = setInterval(() => {
            const initialized = client.isCollectionInitialized(config.typeName, config.key);
            const currentData = client.getCollection(config.typeName, config.key);
            setIsInitialized(initialized);
            setLastUpdate(new Date());
            // Force re-render when data changes (since we're reading directly from singleton)
            forceUpdate({});
        }, 100); // Check for updates every 100ms
        return () => clearInterval(interval);
    }, [client, config.typeName, config.key]);
    // Auto-subscribe when client connects
    useEffect(() => {
        if (!client || !config.autoSubscribe || isSubscribed)
            return;
        const subscribe = async () => {
            try {
                // Add a small delay to ensure connection is fully ready
                await new Promise(resolve => setTimeout(resolve, 100));
                const result = await client.subscribeToType(config.typeName, config.key, config.authString);
                if (result.success) {
                    console.log(`[useAutoPatchCollection] Successfully subscribed to ${config.typeName}`);
                    setIsSubscribed(true);
                    setError(null);
                }
                else {
                    console.error(`[useAutoPatchCollection] Subscription failed for ${config.typeName}:`, result.message);
                    setError(new Error(result.message || 'Subscription failed'));
                }
            }
            catch (err) {
                setError(err instanceof Error ? err : new Error('Subscription error'));
            }
        };
        // Subscribe when client connects
        if (client.getConnectionStatus() === ConnectionStatus.Connected) {
            subscribe();
            return; // No cleanup needed for immediate subscription
        }
        else {
            // Listen for connection changes
            const checkConnection = () => {
                if (client.getConnectionStatus() === ConnectionStatus.Connected && !isSubscribed) {
                    subscribe();
                }
            };
            // Poll for connection status
            const interval = setInterval(checkConnection, 200);
            return () => clearInterval(interval);
        }
    }, [client, config.typeName, config.key, config.authString, config.autoSubscribe, isSubscribed]);
    const subscribe = useCallback(async () => {
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
    const unsubscribe = useCallback(async () => {
        if (!client)
            return;
        try {
            await client.unsubscribeFromType(config.typeName, config.key);
            setIsSubscribed(false);
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
export function createReactHook(defaultConfig) {
    return {
        useAutoPatch: (config, eventHandlers) => useAutoPatch({ ...defaultConfig, ...config }, eventHandlers),
        useAutoPatchCollection: (client, config) => useAutoPatchCollection(client, config)
    };
}
//# sourceMappingURL=react-hook.js.map