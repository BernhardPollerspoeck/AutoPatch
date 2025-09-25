import React from 'react';
import { DeliveryDriver, DriverStatus } from '@autopatch/client';

interface DriverListProps {
  drivers: DeliveryDriver[];
}

const DriverList: React.FC<DriverListProps> = ({ drivers }) => {
  const getStatusColor = (status: DriverStatus) => {
    switch (status) {
      case DriverStatus.Available: return '#10b981';
      case DriverStatus.Assigned: return '#f59e0b';
      case DriverStatus.Delivering: return '#ef4444';
      case DriverStatus.Returning: return '#3b82f6';
      case DriverStatus.Offline: return '#6b7280';
      default: return '#6b7280';
    }
  };

  const getStatusEmoji = (status: DriverStatus) => {
    switch (status) {
      case DriverStatus.Available: return '✅';
      case DriverStatus.Assigned: return '📋';
      case DriverStatus.Delivering: return '🚗';
      case DriverStatus.Returning: return '🔄';
      case DriverStatus.Offline: return '😴';
      default: return '❓';
    }
  };

  if (drivers.length === 0) {
    return (
      <div className="empty-state">
        <div className="empty-state-icon">🚗</div>
        <p>No drivers on duty...</p>
        <p><small>Waiting for data from server</small></p>
      </div>
    );
  }

  return (
    <div className="list-container">
      {drivers.map((driver) => (
        <div key={driver.driverId} className={`driver ${driver.status.toLowerCase()}`}>
          <div className="driver-header">
            <span className="driver-name">{driver.name}</span>
            <span 
              className="driver-status" 
              style={{ 
                backgroundColor: getStatusColor(driver.status) + '20',
                color: getStatusColor(driver.status)
              }}
            >
              {getStatusEmoji(driver.status)} {driver.status}
            </span>
          </div>
          
          <div className="driver-position">
            📍 Position: ({Math.round(driver.x)}, {Math.round(driver.y)})
            {driver.deliverySpeed > 0 && (
              <span> • Speed: {driver.deliverySpeed}px/s</span>
            )}
          </div>
          
          {driver.assignedOrders.length > 0 && (
            <div className="driver-orders">
              📦 Orders: {driver.assignedOrders.join(', ')}
            </div>
          )}
        </div>
      ))}
    </div>
  );
};

export default DriverList;