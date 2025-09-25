/**
 * React hooks for AutoPatch client integration
 * Provides easy-to-use hooks for React applications
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { AutoPatchClient } from '../AutoPatchClient';
import { 
  AutoPatchConfiguration, 
  AutoPatchEventHandlers, 
  ConnectionStatus, 
  Trackable,
  SubscriptionResult
} from '../types';

/**
 * Configuration for the useAutoPatch hook
 */
export interface UseAutoPatchConfig extends AutoPatchConfiguration {
  /** Automatically connect on mount */
  autoConnect?: boolean;
}

/**
 * Return type for the useAutoPatch hook
 */
export interface UseAutoPatchReturn {
  client: AutoPatchClient | null;
  connectionStatus: ConnectionStatus;
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  isConnected: boolean;
  error: Error | null;
}

/**
 * React hook for managing AutoPatch client connection
 */
export function useAutoPatch(
  config: UseAutoPatchConfig,
  eventHandlers?: AutoPatchEventHandlers
): UseAutoPatchReturn {
  const [client, setClient] = useState<AutoPatchClient | null>(null);
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>(ConnectionStatus.Disconnected);
  const [error, setError] = useState<Error | null>(null);
  const clientRef = useRef<AutoPatchClient | null>(null);

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
      } catch (err) {
        setError(err instanceof Error ? err : new Error('Connection failed'));
      }
    }
  }, []);

  const disconnect = useCallback(async () => {
    if (clientRef.current) {
      try {
        await clientRef.current.disconnect();
      } catch (err) {
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
 * Configuration for the useAutoPatchCollection hook
 */
export interface UseAutoPatchCollectionConfig {
  /** The type name to subscribe to */
  typeName: string;
  /** Optional collection key */
  key?: string;
  /** Optional authentication string */
  authString?: string;
  /** Auto-subscribe when client connects */
  autoSubscribe?: boolean;
}

/**
 * Return type for the useAutoPatchCollection hook
 */
export interface UseAutoPatchCollectionReturn<T extends Trackable> {
  data: T[];
  isInitialized: boolean;
  isSubscribed: boolean;
  subscribe: () => Promise<SubscriptionResult>;
  unsubscribe: () => Promise<void>;
  error: Error | null;
  lastUpdate: Date | null;
}

/**
 * React hook for managing a specific data collection
 */
export function useAutoPatchCollection<T extends Trackable>(
  client: AutoPatchClient | null,
  config: UseAutoPatchCollectionConfig
): UseAutoPatchCollectionReturn<T> {
  const [data, setData] = useState<T[]>([]);
  const [isInitialized, setIsInitialized] = useState(false);
  const [isSubscribed, setIsSubscribed] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);

  // Update data when client data changes
  useEffect(() => {
    if (!client) return;

    const interval = setInterval(() => {
      const currentData = client.getCollection<T>(config.typeName, config.key);
      const initialized = client.isCollectionInitialized(config.typeName, config.key);
      
      setData(currentData);
      setIsInitialized(initialized);
      setLastUpdate(new Date());
    }, 100); // Check for updates every 100ms

    return () => clearInterval(interval);
  }, [client, config.typeName, config.key]);

  // Auto-subscribe when client connects
  useEffect(() => {
    if (!client || !config.autoSubscribe) return;

    const subscribe = async () => {
      try {
        const result = await client.subscribeToType<T>(
          config.typeName, 
          config.key, 
          config.authString
        );
        
        if (result.success) {
          setIsSubscribed(true);
          setError(null);
        } else {
          setError(new Error(result.message || 'Subscription failed'));
        }
      } catch (err) {
        setError(err instanceof Error ? err : new Error('Subscription error'));
      }
    };

    // Subscribe when client connects
    if (client.getConnectionStatus() === ConnectionStatus.Connected) {
      subscribe();
    }
  }, [client, config.typeName, config.key, config.authString, config.autoSubscribe]);

  const subscribe = useCallback(async (): Promise<SubscriptionResult> => {
    if (!client) {
      const result = { success: false, message: 'Client not available' };
      setError(new Error(result.message));
      return result;
    }

    try {
      const result = await client.subscribeToType<T>(
        config.typeName, 
        config.key, 
        config.authString
      );
      
      if (result.success) {
        setIsSubscribed(true);
        setError(null);
      } else {
        setError(new Error(result.message || 'Subscription failed'));
      }
      
      return result;
    } catch (err) {
      const error = err instanceof Error ? err : new Error('Subscription error');
      setError(error);
      return { success: false, message: error.message };
    }
  }, [client, config.typeName, config.key, config.authString]);

  const unsubscribe = useCallback(async (): Promise<void> => {
    if (!client) return;

    try {
      await client.unsubscribeFromType(config.typeName, config.key);
      setIsSubscribed(false);
      setData([]);
      setIsInitialized(false);
      setError(null);
    } catch (err) {
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
export function createReactHook(defaultConfig: Partial<AutoPatchConfiguration>) {
  return {
    useAutoPatch: (config: UseAutoPatchConfig, eventHandlers?: AutoPatchEventHandlers) =>
      useAutoPatch({ ...defaultConfig, ...config }, eventHandlers),
    
    useAutoPatchCollection: <T extends Trackable>(
      client: AutoPatchClient | null,
      config: UseAutoPatchCollectionConfig
    ) => useAutoPatchCollection<T>(client, config)
  };
}