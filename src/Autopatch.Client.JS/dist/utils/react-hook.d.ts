/**
 * React hooks for AutoPatch client integration
 * Provides easy-to-use hooks for React applications
 */
import { AutoPatchClient } from '../AutoPatchClient';
import { AutoPatchConfiguration, AutoPatchEventHandlers, ConnectionStatus, Trackable, SubscriptionResult } from '../types';
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
export declare function useAutoPatch(config: UseAutoPatchConfig, eventHandlers?: AutoPatchEventHandlers): UseAutoPatchReturn;
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
export declare function useAutoPatchCollection<T extends Trackable>(client: AutoPatchClient | null, config: UseAutoPatchCollectionConfig): UseAutoPatchCollectionReturn<T>;
/**
 * Factory function to create AutoPatch React hooks with pre-configured settings
 */
export declare function createReactHook(defaultConfig: Partial<AutoPatchConfiguration>): {
    useAutoPatch: (config: UseAutoPatchConfig, eventHandlers?: AutoPatchEventHandlers) => UseAutoPatchReturn;
    useAutoPatchCollection: <T extends Trackable>(client: AutoPatchClient | null, config: UseAutoPatchCollectionConfig) => UseAutoPatchCollectionReturn<T>;
};
//# sourceMappingURL=react-hook.d.ts.map