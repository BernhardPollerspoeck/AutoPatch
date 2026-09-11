import React from 'react'

interface DeliveryDriver {
  driverId: string
  name: string
  status: number
  x: number
  y: number
  deliverySpeed: number
  assignedOrders: string[]
}

interface DeliveryDriversGridProps {
  drivers: DeliveryDriver[]
}

const statusEmojis: Record<number, string> = {
  0: '🟢', // Available
  1: '🟡', // Assigned
  2: '🔴', // Delivering
  3: '🔵', // Returning
  4: '⚫'  // Offline
}

const statusLabels: Record<number, string> = {
  0: 'Available',
  1: 'Assigned',
  2: 'Delivering',
  3: 'Returning',
  4: 'Offline'
}

const DeliveryDriversGrid: React.FC<DeliveryDriversGridProps> = ({ drivers }) => {
  if (!drivers || drivers.length === 0) {
    return (
      <div className="grid-container">
        <div className="empty-state">
          <p>No delivery drivers available... 🚗</p>
        </div>
      </div>
    )
  }

  const getLocationDescription = (driver: DeliveryDriver): string => {
    const { x, status } = driver
    
    // The server sends enums as numbers: 0 Available, 1 Assigned, 2 Delivering, 3 Returning, 4 Offline
    if (status === 0 || status === 1) {
      return 'At Restaurant'
    } else if (status === 2) {
      const progress = Math.round(((x - 50) / (750 - 50)) * 100)
      return `En Route (${Math.max(0, Math.min(100, progress))}%)`
    } else if (status === 3) {
      const progress = Math.round(((750 - x) / (750 - 50)) * 100)
      return `Returning (${Math.max(0, Math.min(100, progress))}%)`
    }
    
    return 'Unknown'
  }

  return (
    <div className="grid-container">
      <table className="data-grid">
        <thead>
          <tr>
            <th>Driver ID</th>
            <th>Name</th>
            <th>Status</th>
            <th>Location</th>
            <th>Speed</th>
            <th>Assigned Orders</th>
          </tr>
        </thead>
        <tbody>
          {drivers.map((driver) => (
            <tr key={driver.driverId} className={`status-${driver.status}`}>
              <td className="driver-id">{driver.driverId}</td>
              <td className="driver-name">{driver.name}</td>
              <td className="status">
                <span className="status-badge">
                  {statusEmojis[driver.status] || '❓'} {statusLabels[driver.status] || driver.status}
                </span>
              </td>
              <td className="location">
                {getLocationDescription(driver)}
              </td>
              <td className="speed">
                {driver.deliverySpeed} px/s
              </td>
              <td className="assigned-orders">
                <div className="orders-list">
                  {driver.assignedOrders?.length > 0 ? (
                    driver.assignedOrders.map((orderId, index) => (
                      <span key={index} className="order-tag">{orderId}</span>
                    ))
                  ) : (
                    <span className="no-orders">None</span>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default DeliveryDriversGrid