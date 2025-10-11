import React from 'react'

interface PizzaOrder {
  orderId: string
  customerName: string
  items: string[]
  status: number
  orderTime: string
  estimatedDelivery?: string
  assignedDriverId?: string
  assignedDriverName?: string
}

interface PizzaOrdersGridProps {
  orders: PizzaOrder[]
}

const statusEmojis: Record<number, string> = {
  0: '📋', // Received
  1: '🔪', // Preparing  
  2: '🔥', // Baking
  3: '✅', // Ready
  4: '🚗', // OutForDelivery
  5: '🎉'  // Delivered
}

const statusNames: Record<number, string> = {
  0: 'Received',
  1: 'Preparing',
  2: 'Baking', 
  3: 'Ready',
  4: 'Out for Delivery',
  5: 'Delivered'
}

const PizzaOrdersGrid: React.FC<PizzaOrdersGridProps> = ({ orders }) => {
  if (!orders || orders.length === 0) {
    return (
      <div className="grid-container">
        <div className="empty-state">
          <p>No pizza orders yet... 🍕</p>
        </div>
      </div>
    )
  }

  const formatTime = (timeString: string) => {
    try {
      const date = new Date(timeString)
      return date.toLocaleTimeString('en-US', { 
        hour: '2-digit', 
        minute: '2-digit',
        second: '2-digit'
      })
    } catch {
      return timeString
    }
  }

  return (
    <div className="grid-container">
      <table className="data-grid">
        <thead>
          <tr>
            <th>Order ID</th>
            <th>Customer</th>
            <th>Items</th>
            <th>Status</th>
            <th>Order Time</th>
            <th>Est. Delivery</th>
            <th>Assigned Driver</th>
          </tr>
        </thead>
        <tbody>
          {orders.map((order) => (
            <tr key={order.orderId} className={`status-${order.status}`}>
              <td className="order-id">{order.orderId}</td>
              <td className="customer-name">{order.customerName}</td>
              <td className="items">
                <div className="items-list">
                  {order.items?.map((item, index) => (
                    <span key={index} className="item-tag">{item}</span>
                  )) || 'No items'}
                </div>
              </td>
              <td className="status">
                <span className="status-badge">
                  {statusEmojis[order.status] || '❓'} {statusNames[order.status] || `Status ${order.status}`}
                </span>
              </td>
              <td className="order-time">{formatTime(order.orderTime)}</td>
              <td className="estimated-delivery">
                {order.estimatedDelivery ? formatTime(order.estimatedDelivery) : '-'}
              </td>
              <td className="assigned-driver">
                {order.assignedDriverName || '-'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default PizzaOrdersGrid