import React from 'react';
import { PizzaOrder, OrderStatus } from '@autopatch/client';

interface PizzaOrderListProps {
  orders: PizzaOrder[];
}

const PizzaOrderList: React.FC<PizzaOrderListProps> = ({ orders }) => {
  const getStatusColor = (status: OrderStatus) => {
    switch (status) {
      case OrderStatus.Received: return '#3b82f6';
      case OrderStatus.Preparing: return '#f59e0b';
      case OrderStatus.Baking: return '#ef4444';
      case OrderStatus.Ready: return '#10b981';
      case OrderStatus.OutForDelivery: return '#8b5cf6';
      case OrderStatus.Delivered: return '#6b7280';
      default: return '#6b7280';
    }
  };

  const formatTime = (dateString: string) => {
    return new Date(dateString).toLocaleTimeString();
  };

  const getStatusEmoji = (status: OrderStatus) => {
    switch (status) {
      case OrderStatus.Received: return '📝';
      case OrderStatus.Preparing: return '👨‍🍳';
      case OrderStatus.Baking: return '🔥';
      case OrderStatus.Ready: return '✅';
      case OrderStatus.OutForDelivery: return '🚗';
      case OrderStatus.Delivered: return '🎉';
      default: return '❓';
    }
  };

  if (orders.length === 0) {
    return (
      <div className="empty-state">
        <div className="empty-state-icon">🍕</div>
        <p>No pizza orders yet...</p>
        <p><small>Waiting for data from server</small></p>
      </div>
    );
  }

  return (
    <div className="list-container">
      {orders.map((order) => (
        <div key={order.orderId} className={`pizza-order ${order.status.toLowerCase().replace('fordelivery', '-for-delivery')}`}>
          <div className="order-header">
            <span className="order-id">{order.orderId}</span>
            <span 
              className="order-status" 
              style={{ 
                backgroundColor: getStatusColor(order.status) + '20',
                color: getStatusColor(order.status)
              }}
            >
              {getStatusEmoji(order.status)} {order.status}
            </span>
          </div>
          
          <div className="order-customer">
            👤 {order.customerName}
          </div>
          
          <div className="order-items">
            🍕 {order.items.join(', ')}
          </div>
          
          <div className="order-meta">
            <span>🕐 {formatTime(order.orderTime)}</span>
            {order.assignedDriverName && (
              <span>🚗 {order.assignedDriverName}</span>
            )}
          </div>
          
          {order.estimatedDelivery && (
            <div className="order-meta">
              <span>📅 ETA: {formatTime(order.estimatedDelivery)}</span>
            </div>
          )}
        </div>
      ))}
    </div>
  );
};

export default PizzaOrderList;