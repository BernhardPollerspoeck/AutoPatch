using CommunityToolkit.Mvvm.ComponentModel;

namespace Autopatch.Demo.Shared;

/// <summary>
/// Represents a pizza order in Poller's Pizza Palace tracking system.
/// Tracks the complete lifecycle from order receipt through delivery completion.
/// </summary>
public partial class PizzaOrder : ObservableObject
{
    /// <summary>
    /// Unique identifier for the order (e.g., "PPP-1234")
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Customer name for the order
    /// </summary>
    [ObservableProperty]
    private string _customerName = string.Empty;

    /// <summary>
    /// List of ordered items (pizzas, drinks, etc.)
    /// </summary>
    [ObservableProperty]
    private List<string> _items = [];

    /// <summary>
    /// Current status in the order pipeline
    /// </summary>
    [ObservableProperty]
    private OrderStatus _status = OrderStatus.Received;

    /// <summary>
    /// When the order was placed
    /// </summary>
    [ObservableProperty]
    private DateTime _orderTime = DateTime.Now;

    /// <summary>
    /// Estimated delivery time (calculated based on status and driver assignment)
    /// </summary>
    [ObservableProperty]
    private DateTime? _estimatedDelivery;

    /// <summary>
    /// ID of the assigned driver (when status is OutForDelivery)
    /// </summary>
    [ObservableProperty]
    private string? _assignedDriverId;

    /// <summary>
    /// Name of the assigned driver for display purposes
    /// </summary>
    [ObservableProperty]
    private string? _assignedDriverName;
}

/// <summary>
/// Order status pipeline: Received → Preparing → Baking → Ready → OutForDelivery → Delivered
/// </summary>
public enum OrderStatus
{
    /// <summary>Order just received from customer</summary>
    Received,
    /// <summary>Kitchen is preparing ingredients</summary>
    Preparing,
    /// <summary>Pizza is in the oven</summary>
    Baking,
    /// <summary>Order is ready for pickup by driver</summary>
    Ready,
    /// <summary>Driver has picked up and is delivering</summary>
    OutForDelivery,
    /// <summary>Order has been delivered (will be removed from system)</summary>
    Delivered
}

/// <summary>
/// Represents a pizza delivery driver in Poller's Pizza Palace tracking system.
/// Tracks driver position and status for live delivery visualization.
/// </summary>
public partial class DeliveryDriver : ObservableObject
{
    /// <summary>
    /// Unique identifier for the driver (e.g., "DRV-A", "DRV-B")
    /// </summary>
    public string DriverId { get; set; } = string.Empty;

    /// <summary>
    /// Driver's display name
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// Current driver status in the delivery workflow
    /// </summary>
    [ObservableProperty]
    private DriverStatus _status = DriverStatus.Available;

    /// <summary>
    /// X coordinate on the delivery map canvas (50=Restaurant, 750=Customer)
    /// Links→Rechts movement represents restaurant to customer delivery
    /// </summary>
    [ObservableProperty]
    private double _x = 50; // Default: At restaurant

    /// <summary>
    /// Y coordinate on the delivery map canvas (250-350 range to avoid overlap)
    /// Each driver has a fixed Y position to prevent visual collision
    /// </summary>
    [ObservableProperty]
    private double _y = 300; // Default: Middle lane

    /// <summary>
    /// Movement speed in pixels per second (80-150, represents delivery distance)
    /// Higher speed = closer customer, lower speed = farther customer
    /// Used for time-based movement calculations independent of frame rate
    /// </summary>
    [ObservableProperty]
    private int _deliverySpeed = 2;

    /// <summary>
    /// List of order IDs currently assigned to this driver (max 2 orders)
    /// </summary>
    [ObservableProperty]
    private List<string> _assignedOrders = [];
}

/// <summary>
/// Driver status pipeline: Available → Assigned → Delivering → Returning → Available
/// </summary>
public enum DriverStatus
{
    /// <summary>Driver is at restaurant and available for orders (Green, X=50)</summary>
    Available,
    /// <summary>Driver has been assigned an order and is preparing to leave (Orange, X=50)</summary>
    Assigned,
    /// <summary>Driver is delivering to customer (Red, X: 50→750)</summary>
    Delivering,
    /// <summary>Driver is returning to restaurant (Blue, X: 750→50)</summary>
    Returning,
    /// <summary>Driver is offline and not visible on map</summary>
    Offline
}
