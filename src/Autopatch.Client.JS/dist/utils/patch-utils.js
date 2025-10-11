/**
 * Utility functions for working with JSON Patch operations
 */
import { compare, applyPatch, deepClone } from 'fast-json-patch';
/**
 * Utility class for JSON Patch operations
 */
export class PatchUtils {
    /**
     * Applies a set of JSON Patch operations to an array of objects
     */
    static applyOperationsToArray(items, operations) {
        const result = deepClone(items);
        const errors = [];
        try {
            const patchResult = applyPatch(result, operations, false, false);
            // Check for any failed operations
            patchResult.forEach((opResult, index) => {
                if (opResult.test === false) {
                    errors.push(`Operation ${index} failed: ${JSON.stringify(operations[index])}`);
                }
            });
            return { result, errors };
        }
        catch (error) {
            errors.push(`Patch application failed: ${error instanceof Error ? error.message : 'Unknown error'}`);
            return { result: items, errors };
        }
    }
    /**
     * Compares two arrays and generates JSON Patch operations
     */
    static compareArrays(oldArray, newArray) {
        return compare(oldArray, newArray);
    }
    /**
     * Finds an item in an array by a key property
     */
    static findItemByKey(items, keyProperty, keyValue) {
        return items.find(item => item[keyProperty] === keyValue);
    }
    /**
     * Updates or adds an item to an array based on a key property
     */
    static upsertItem(items, newItem, keyProperty) {
        const index = items.findIndex(item => item[keyProperty] === newItem[keyProperty]);
        if (index >= 0) {
            // Update existing item
            const updated = [...items];
            updated[index] = newItem;
            return updated;
        }
        else {
            // Add new item
            return [...items, newItem];
        }
    }
    /**
     * Removes an item from an array based on a key property
     */
    static removeItem(items, keyProperty, keyValue) {
        return items.filter(item => item[keyProperty] !== keyValue);
    }
    /**
     * Validates that a JSON Patch operation is safe to apply
     */
    static isOperationSafe(operation) {
        // Basic validation - can be extended based on security requirements
        if (!operation.path || typeof operation.path !== 'string') {
            return false;
        }
        // Don't allow operations on prototype or constructor
        if (operation.path.includes('__proto__') ||
            operation.path.includes('constructor') ||
            operation.path.includes('prototype')) {
            return false;
        }
        return true;
    }
    /**
     * Filters operations to only include safe ones
     */
    static filterSafeOperations(operations) {
        return operations.filter(this.isOperationSafe);
    }
    /**
     * Converts a partial object update to JSON Patch operations
     */
    static createUpdateOperations(basePath, updates) {
        const operations = [];
        Object.entries(updates).forEach(([key, value]) => {
            operations.push({
                op: 'replace',
                path: `${basePath}/${key}`,
                value
            });
        });
        return operations;
    }
    /**
     * Creates operations to add a new item to an array
     */
    static createAddOperation(basePath, item, index) {
        const path = index !== undefined ? `${basePath}/${index}` : `${basePath}/-`;
        return {
            op: 'add',
            path,
            value: item
        };
    }
    /**
     * Creates operation to remove an item from an array
     */
    static createRemoveOperation(basePath, index) {
        return {
            op: 'remove',
            path: `${basePath}/${index}`
        };
    }
    /**
     * Sorts operations to ensure adds come after removes for consistency
     */
    static sortOperations(operations) {
        // Sort by operation type: remove, replace, add
        const priority = {
            remove: 0,
            replace: 1,
            add: 2,
            move: 1,
            copy: 1,
            test: 3,
            _get: 4 // Handle internal fast-json-patch operation
        };
        return [...operations].sort((a, b) => {
            const aPriority = priority[a.op] ?? 99;
            const bPriority = priority[b.op] ?? 99;
            if (aPriority !== bPriority) {
                return aPriority - bPriority;
            }
            // For same operation types, sort by path for consistency
            return a.path.localeCompare(b.path);
        });
    }
    /**
     * Merges multiple operation arrays while removing duplicates
     */
    static mergeOperations(...operationArrays) {
        const allOperations = operationArrays.flat();
        const operationMap = new Map();
        // Use path + op as key to detect duplicates
        allOperations.forEach(op => {
            const key = `${op.op}:${op.path}`;
            operationMap.set(key, op); // Later operations override earlier ones
        });
        return this.sortOperations(Array.from(operationMap.values()));
    }
}
//# sourceMappingURL=patch-utils.js.map