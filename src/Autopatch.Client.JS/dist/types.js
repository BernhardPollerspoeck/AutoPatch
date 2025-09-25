"use strict";
/**
 * AutoPatch Client Types
 * TypeScript interfaces mirroring the .NET AutoPatch models
 */
Object.defineProperty(exports, "__esModule", { value: true });
exports.DriverStatus = exports.OrderStatus = exports.ConnectionStatus = void 0;
/**
 * Connection status for the AutoPatch client
 */
var ConnectionStatus;
(function (ConnectionStatus) {
    ConnectionStatus["Disconnected"] = "Disconnected";
    ConnectionStatus["Connecting"] = "Connecting";
    ConnectionStatus["Connected"] = "Connected";
    ConnectionStatus["Reconnecting"] = "Reconnecting";
    ConnectionStatus["Disconnecting"] = "Disconnecting";
})(ConnectionStatus || (exports.ConnectionStatus = ConnectionStatus = {}));
/**
 * Order status enumeration (demo)
 */
var OrderStatus;
(function (OrderStatus) {
    OrderStatus["Received"] = "Received";
    OrderStatus["Preparing"] = "Preparing";
    OrderStatus["Baking"] = "Baking";
    OrderStatus["Ready"] = "Ready";
    OrderStatus["OutForDelivery"] = "OutForDelivery";
    OrderStatus["Delivered"] = "Delivered";
})(OrderStatus || (exports.OrderStatus = OrderStatus = {}));
/**
 * Driver status enumeration (demo)
 */
var DriverStatus;
(function (DriverStatus) {
    DriverStatus["Available"] = "Available";
    DriverStatus["Assigned"] = "Assigned";
    DriverStatus["Delivering"] = "Delivering";
    DriverStatus["Returning"] = "Returning";
    DriverStatus["Offline"] = "Offline";
})(DriverStatus || (exports.DriverStatus = DriverStatus = {}));
//# sourceMappingURL=types.js.map