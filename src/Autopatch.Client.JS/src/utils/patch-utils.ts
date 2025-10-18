/**
 * Utility functions for working with JSON Patch operations
 */

import { Operation, Trackable } from '../types';
import { compare, applyPatch, deepClone, Operation as JsonPatchOperation } from 'fast-json-patch';

/**
 * Utility class for JSON Patch operations
 */
export class PatchUtils {
  /**
   * Applies a set of JSON Patch operations to an array of objects
   */
  static applyOperationsToArray<T extends Trackable>(
    items: T[], 
    operations: Operation[]
  ): { result: T[]; errors: string[] } {
    const result = deepClone(items);
    const errors: string[] = [];

    try {
      const patchResult = applyPatch(result, operations as readonly JsonPatchOperation[], false, false);
      
      // Check for any failed operations
      patchResult.forEach((opResult, index) => {
        if (opResult.test === false) {
          errors.push(`Operation ${index} failed: ${JSON.stringify(operations[index])}`);
        }
      });

      return { result, errors };
    } catch (error) {
      errors.push(`Patch application failed: ${error instanceof Error ? error.message : 'Unknown error'}`);
      return { result: items, errors };
    }
  }

  /**
   * Compares two arrays and generates JSON Patch operations
   */
  static compareArrays<T extends Trackable>(
    oldArray: T[], 
    newArray: T[]
  ): Operation[] {
    return compare(oldArray, newArray) as Operation[];
  }

  /**
   * Finds an item in an array by a key property
   */
  static findItemByKey<T extends Trackable>(
    items: T[], 
    keyProperty: keyof T, 
    keyValue: any
  ): T | undefined {
    return items.find(item => item[keyProperty] === keyValue);
  }

  /**
   * Updates or adds an item to an array based on a key property
   */
  static upsertItem<T extends Trackable>(
    items: T[], 
    newItem: T, 
    keyProperty: keyof T
  ): T[] {
    const index = items.findIndex(item => item[keyProperty] === newItem[keyProperty]);
    
    if (index >= 0) {
      // Update existing item
      const updated = [...items];
      updated[index] = newItem;
      return updated;
    } else {
      // Add new item
      return [...items, newItem];
    }
  }

  /**
   * Removes an item from an array based on a key property
   */
  static removeItem<T extends Trackable>(
    items: T[], 
    keyProperty: keyof T, 
    keyValue: any
  ): T[] {
    return items.filter(item => item[keyProperty] !== keyValue);
  }

  /**
   * Validates that a JSON Patch operation is safe to apply
   */
  static isOperationSafe(operation: Operation): boolean {
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
  static filterSafeOperations(operations: Operation[]): Operation[] {
    return operations.filter(this.isOperationSafe);
  }

  /**
   * Converts a partial object update to JSON Patch operations
   */
  static createUpdateOperations<T extends Trackable>(
    basePath: string,
    updates: Partial<T>
  ): Operation[] {
    const operations: Operation[] = [];

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
  static createAddOperation<T extends Trackable>(
    basePath: string,
    item: T,
    index?: number
  ): Operation {
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
  static createRemoveOperation(basePath: string, index: number): Operation {
    return {
      op: 'remove',
      path: `${basePath}/${index}`
    };
  }

  /**
   * Sorts operations to ensure adds come after removes for consistency
   */
  static sortOperations(operations: Operation[]): Operation[] {
    // Sort by operation type: remove, replace, add
    const priority: Record<string, number> = { 
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
  static mergeOperations(...operationArrays: Operation[][]): Operation[] {
    const allOperations = operationArrays.flat();
    const operationMap = new Map<string, Operation>();

    // Use path + op as key to detect duplicates
    allOperations.forEach(op => {
      const key = `${op.op}:${op.path}`;
      operationMap.set(key, op); // Later operations override earlier ones
    });

    return this.sortOperations(Array.from(operationMap.values()));
  }
}