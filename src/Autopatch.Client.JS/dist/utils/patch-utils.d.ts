/**
 * Utility functions for working with JSON Patch operations
 */
import { Operation, Trackable } from '../types';
/**
 * Utility class for JSON Patch operations
 */
export declare class PatchUtils {
    /**
     * Applies a set of JSON Patch operations to an array of objects
     */
    static applyOperationsToArray<T extends Trackable>(items: T[], operations: Operation[]): {
        result: T[];
        errors: string[];
    };
    /**
     * Compares two arrays and generates JSON Patch operations
     */
    static compareArrays<T extends Trackable>(oldArray: T[], newArray: T[]): Operation[];
    /**
     * Finds an item in an array by a key property
     */
    static findItemByKey<T extends Trackable>(items: T[], keyProperty: keyof T, keyValue: any): T | undefined;
    /**
     * Updates or adds an item to an array based on a key property
     */
    static upsertItem<T extends Trackable>(items: T[], newItem: T, keyProperty: keyof T): T[];
    /**
     * Removes an item from an array based on a key property
     */
    static removeItem<T extends Trackable>(items: T[], keyProperty: keyof T, keyValue: any): T[];
    /**
     * Validates that a JSON Patch operation is safe to apply
     */
    static isOperationSafe(operation: Operation): boolean;
    /**
     * Filters operations to only include safe ones
     */
    static filterSafeOperations(operations: Operation[]): Operation[];
    /**
     * Converts a partial object update to JSON Patch operations
     */
    static createUpdateOperations<T extends Trackable>(basePath: string, updates: Partial<T>): Operation[];
    /**
     * Creates operations to add a new item to an array
     */
    static createAddOperation<T extends Trackable>(basePath: string, item: T, index?: number): Operation;
    /**
     * Creates operation to remove an item from an array
     */
    static createRemoveOperation(basePath: string, index: number): Operation;
    /**
     * Sorts operations to ensure adds come after removes for consistency
     */
    static sortOperations(operations: Operation[]): Operation[];
    /**
     * Merges multiple operation arrays while removing duplicates
     */
    static mergeOperations(...operationArrays: Operation[][]): Operation[];
}
//# sourceMappingURL=patch-utils.d.ts.map