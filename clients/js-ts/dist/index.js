"use strict";
/**
 * AutoPatch JavaScript/TypeScript Client
 *
 * A client library for connecting to the AutoPatch real-time synchronization framework.
 * Provides seamless integration with SignalR hubs and automatic JSON Patch operations.
 *
 * @example
 * ```typescript
 * import { AutoPatchClient, ConnectionStatus } from '@autopatch/client';
 *
 * const client = new AutoPatchClient({
 *   endpoint: 'http://localhost:5249/Autopatch'
 * }, {
 *   onConnectionChanged: (status) => console.log('Connection:', status),
 *   onDataReceived: (typeName, data) => console.log('Data:', typeName, data)
 * });
 *
 * await client.connect();
 * await client.subscribeToType('PizzaOrder');
 * ```
 */
Object.defineProperty(exports, "__esModule", { value: true });
exports.VERSION = exports.useAutoPatchCollection = exports.useAutoPatch = exports.createReactHook = exports.PatchUtils = exports.DriverStatus = exports.OrderStatus = exports.ConnectionStatus = exports.AutoPatchClient = void 0;
// Main client exports
var AutoPatchClient_1 = require("./AutoPatchClient");
Object.defineProperty(exports, "AutoPatchClient", { enumerable: true, get: function () { return AutoPatchClient_1.AutoPatchClient; } });
// Enums
var types_1 = require("./types");
Object.defineProperty(exports, "ConnectionStatus", { enumerable: true, get: function () { return types_1.ConnectionStatus; } });
Object.defineProperty(exports, "OrderStatus", { enumerable: true, get: function () { return types_1.OrderStatus; } });
Object.defineProperty(exports, "DriverStatus", { enumerable: true, get: function () { return types_1.DriverStatus; } });
// Utilities
var patch_utils_1 = require("./utils/patch-utils");
Object.defineProperty(exports, "PatchUtils", { enumerable: true, get: function () { return patch_utils_1.PatchUtils; } });
// React hooks (conditionally exported)
let reactHookExports = {};
try {
    // Only export React hooks if React is available
    const reactHooks = require('./utils/react-hook');
    reactHookExports = {
        createReactHook: reactHooks.createReactHook,
        useAutoPatch: reactHooks.useAutoPatch,
        useAutoPatchCollection: reactHooks.useAutoPatchCollection
    };
}
catch (error) {
    // React not available, skip React hook exports
}
exports.createReactHook = reactHookExports.createReactHook, exports.useAutoPatch = reactHookExports.useAutoPatch, exports.useAutoPatchCollection = reactHookExports.useAutoPatchCollection;
// Version
exports.VERSION = '1.0.0';
//# sourceMappingURL=index.js.map