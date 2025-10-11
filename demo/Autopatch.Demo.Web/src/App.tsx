import { useAutoPatch, useAutoPatchCollection } from '@autopatch/client'
import PizzaOrdersGrid from './components/PizzaOrdersGrid'
import DeliveryDriversGrid from './components/DeliveryDriversGrid'
import ConnectionStatus from './components/ConnectionStatus'
import './App.css'

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

interface DeliveryDriver {
  driverId: string
  name: string
  status: number
  x: number
  y: number
  deliverySpeed: number
  assignedOrders: string[]
}

function App() {
  const { client, isConnected, error: connectionError } = useAutoPatch({
    endpoint: 'http://localhost:5249/autopatch',
    autoConnect: true
  })

  const {
    data: pizzaOrders,
    error: pizzaOrdersError
  } = useAutoPatchCollection<PizzaOrder>(client, {
    typeName: 'PizzaOrder',
    autoSubscribe: true
  })

  const {
    data: deliveryDrivers,
    error: driversError
  } = useAutoPatchCollection<DeliveryDriver>(client, {
    typeName: 'DeliveryDriver',
    autoSubscribe: true
  })

  const hasError = connectionError || pizzaOrdersError || driversError

  return (
    <div className="app">
      <header className="app-header">
        <h1>🍕 Poller's Pizza Palace - Live Tracking Demo</h1>
        <ConnectionStatus 
          isConnected={isConnected} 
          error={hasError ? (connectionError || pizzaOrdersError || driversError) : null} 
        />
      </header>
      
      <main className="app-main">
        <div className="grids-container">
          <div className="grid-section">
            <h2>📋 Pizza Orders</h2>
            <PizzaOrdersGrid orders={pizzaOrders} />
          </div>
          
          <div className="grid-section">
            <h2>🚗 Delivery Drivers</h2>
            <DeliveryDriversGrid drivers={deliveryDrivers} />
          </div>
        </div>
      </main>
    </div>
  )
}

export default App
