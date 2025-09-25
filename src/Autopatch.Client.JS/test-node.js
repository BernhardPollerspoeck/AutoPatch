/**
 * Node.js test script for AutoPatch client
 * Tests basic connection and subscription functionality
 */

const { AutoPatchClient, ConnectionStatus } = require('./dist/AutoPatchClient');

async function testClient() {
  console.log('🧪 Starting AutoPatch client test...\n');

  const client = new AutoPatchClient({
    endpoint: 'http://localhost:5249/Autopatch'
  }, {
    onConnectionChanged: (status) => {
      console.log(`🔗 Connection status changed: ${status}`);
    },
    onError: (error) => {
      console.error(`❌ Error: ${error.message}`);
    },
    onDataReceived: (typeName, data, isInitialData) => {
      console.log(`📊 Received ${data.length} ${typeName} items ${isInitialData ? '(initial)' : '(update)'}`);
      if (typeName === 'PizzaOrder' && data.length > 0) {
        console.log(`   Latest order: ${data[data.length - 1].orderId} - ${data[data.length - 1].customerName} - ${data[data.length - 1].status}`);
      }
      if (typeName === 'DeliveryDriver' && data.length > 0) {
        const available = data.filter(d => d.status === 'Available').length;
        const delivering = data.filter(d => d.status === 'Delivering').length;
        console.log(`   Drivers: ${available} available, ${delivering} delivering`);
      }
    },
    onDataUpdated: (typeName, operations, isInitialData) => {
      if (!isInitialData) {
        console.log(`🔄 ${typeName} updated with ${operations.length} operations`);
      }
    }
  });

  try {
    // Connect to server
    console.log('🔌 Connecting to AutoPatch server...');
    await client.connect();
    console.log('✅ Connected successfully!\n');

    // Subscribe to PizzaOrder with authentication
    console.log('📋 Subscribing to PizzaOrder with admin auth...');
    const pizzaResult = await client.subscribeToType('PizzaOrder', null, 'admin');
    if (pizzaResult.success) {
      console.log('✅ PizzaOrder subscription successful');
    } else {
      console.log(`❌ PizzaOrder subscription failed: ${pizzaResult.message}`);
    }

    // Subscribe to DeliveryDriver
    console.log('🚗 Subscribing to DeliveryDriver...');
    const driverResult = await client.subscribeToType('DeliveryDriver');
    if (driverResult.success) {
      console.log('✅ DeliveryDriver subscription successful');
    } else {
      console.log(`❌ DeliveryDriver subscription failed: ${driverResult.message}`);
    }

    console.log('\n🎯 Subscriptions complete. Monitoring data for 30 seconds...\n');

    // Monitor for 30 seconds
    let updateCount = 0;
    const interval = setInterval(() => {
      updateCount++;
      
      const orders = client.getCollection('PizzaOrder');
      const drivers = client.getCollection('DeliveryDriver');
      
      console.log(`📊 Update ${updateCount}: ${orders.length} orders, ${drivers.length} drivers`);
      
      if (orders.length > 0) {
        const statusCounts = orders.reduce((acc, order) => {
          acc[order.status] = (acc[order.status] || 0) + 1;
          return acc;
        }, {});
        console.log(`   Order statuses: ${JSON.stringify(statusCounts)}`);
      }
    }, 5000);

    // Stop after 30 seconds
    setTimeout(async () => {
      clearInterval(interval);
      console.log('\n🛑 Test complete. Disconnecting...');
      
      await client.unsubscribeFromType('PizzaOrder');
      await client.unsubscribeFromType('DeliveryDriver');
      await client.disconnect();
      
      console.log('✅ Test completed successfully!');
      process.exit(0);
    }, 30000);

  } catch (error) {
    console.error(`💥 Test failed: ${error.message}`);
    process.exit(1);
  }
}

// Handle process termination
process.on('SIGINT', async () => {
  console.log('\n🛑 Received SIGINT, cleaning up...');
  process.exit(0);
});

process.on('unhandledRejection', (reason, promise) => {
  console.error('💥 Unhandled Rejection at:', promise, 'reason:', reason);
  process.exit(1);
});

// Run the test
testClient().catch(error => {
  console.error(`💥 Test startup failed: ${error.message}`);
  process.exit(1);
});