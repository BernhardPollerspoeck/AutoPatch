/**
 * AutoPatch Client Types
 * TypeScript interfaces mirroring the .NET AutoPatch models
 */
/**
 * Connection status for the AutoPatch client
 */
export var ConnectionStatus;
(function (ConnectionStatus) {
    ConnectionStatus["Disconnected"] = "Disconnected";
    ConnectionStatus["Connecting"] = "Connecting";
    ConnectionStatus["Connected"] = "Connected";
    ConnectionStatus["Reconnecting"] = "Reconnecting";
    ConnectionStatus["Disconnecting"] = "Disconnecting";
})(ConnectionStatus || (ConnectionStatus = {}));
/**
 * Order status enumeration (demo)
 */
export var OrderStatus;
(function (OrderStatus) {
    OrderStatus["Received"] = "Received";
    OrderStatus["Preparing"] = "Preparing";
    OrderStatus["Baking"] = "Baking";
    OrderStatus["Ready"] = "Ready";
    OrderStatus["OutForDelivery"] = "OutForDelivery";
    OrderStatus["Delivered"] = "Delivered";
})(OrderStatus || (OrderStatus = {}));
/**
 * Driver status enumeration (demo)
 */
export var DriverStatus;
(function (DriverStatus) {
    DriverStatus["Available"] = "Available";
    DriverStatus["Assigned"] = "Assigned";
    DriverStatus["Delivering"] = "Delivering";
    DriverStatus["Returning"] = "Returning";
    DriverStatus["Offline"] = "Offline";
})(DriverStatus || (DriverStatus = {}));
//# sourceMappingURL=types.js.map