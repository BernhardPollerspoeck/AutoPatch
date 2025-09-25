import React from 'react';
import { 
  AutoPatchClient, 
  ConnectionStatus, 
  PizzaOrder, 
  DeliveryDriver,
  OrderStatus,
  DriverStatus
} from '@autopatch/client';
import PizzaOrderList from './components/PizzaOrderList';
import DriverList from './components/DriverList';
import ConnectionIndicator from './components/ConnectionIndicator';
import './App.css';

const App: React.FC = () => {
  const [client, setClient] = React.useState<AutoPatchClient | null>(null);
  const [connectionStatus, setConnectionStatus] = React.useState<ConnectionStatus>(ConnectionStatus.Disconnected);
  const [pizzaOrders, setPizzaOrders] = React.useState<PizzaOrder[]>([]);
  const [drivers, setDrivers] = React.useState<DeliveryDriver[]>([]);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    // Create AutoPatch client
    const autoPatchClient = new AutoPatchClient({
      endpoint: 'http://localhost:5249/Autopatch'
    }, {
      onConnectionChanged: (status) => {
        setConnectionStatus(status);
        console.log('Connection status:', status);
      },
      onError: (err) => {
        setError(err.message);
        console.error('AutoPatch error:', err);
      },
      onDataReceived: (typeName, data, isInitialData) => {
        console.log(`Data received for ${typeName}:`, data.length, 'items', isInitialData ? '(initial)' : '(update)');
        
        if (typeName === 'PizzaOrder') {
          setPizzaOrders(data as PizzaOrder[]);
        } else if (typeName === 'DeliveryDriver') {
          setDrivers(data as DeliveryDriver[]);
        }
      }
    });

    setClient(autoPatchClient);

    // Connect and subscribe
    const initialize = async () => {
      try {
        await autoPatchClient.connect();
        
        // Subscribe to pizza orders
        const pizzaResult = await autoPatchClient.subscribeToType<PizzaOrder>('PizzaOrder');
        if (!pizzaResult.success) {
          setError(`Failed to subscribe to PizzaOrder: ${pizzaResult.message}`);
        }
        
        // Subscribe to delivery drivers
        const driverResult = await autoPatchClient.subscribeToType<DeliveryDriver>('DeliveryDriver');
        if (!driverResult.success) {
          setError(`Failed to subscribe to DeliveryDriver: ${driverResult.message}`);
        }
      } catch (err) {
        setError(`Failed to initialize: ${err instanceof Error ? err.message : 'Unknown error'}`);
      }
    };

    initialize();

    // Cleanup on unmount
    return () => {
      autoPatchClient.dispose();
    };
  }, []);

  // Periodically check for updated data
  React.useEffect(() => {
    if (!client) return;

    const interval = setInterval(() => {
      const currentPizzaOrders = client.getCollection<PizzaOrder>('PizzaOrder');
      const currentDrivers = client.getCollection<DeliveryDriver>('DeliveryDriver');
      
      setPizzaOrders([...currentPizzaOrders]);
      setDrivers([...currentDrivers]);
    }, 500); // Update every 500ms

    return () => clearInterval(interval);
  }, [client]);

  const clearError = () => setError(null);

  return (
    <div className="app">
      <header className="app-header">
        <h1>🍕 Poller's Pizza Palace - Live Demo</h1>
        <p>Real-time order and driver tracking with AutoPatch</p>
        <ConnectionIndicator status={connectionStatus} />
      </header>

      {error && (
        <div className="error-banner">
          <span>❌ {error}</span>
          <button onClick={clearError}>✕</button>
        </div>
      )}

      <main className="app-main">
        <div className="dashboard-grid">
          <section className="orders-section">
            <h2>📋 Pizza Orders ({pizzaOrders.length})</h2>
            <PizzaOrderList orders={pizzaOrders} />
          </section>

          <section className="drivers-section">
            <h2>🚗 Delivery Drivers ({drivers.length})</h2>
            <DriverList drivers={drivers} />
          </section>
        </div>
      </main>

      <footer className="app-footer">
        <p>
          Powered by <strong>AutoPatch</strong> - Real-time data synchronization framework
        </p>
      </footer>
    </div>
  );
};

export default App;