/**
 * Debug test script to see what data is being received
 */

const { AutoPatchClient } = require('./dist/AutoPatchClient');

async function debugTest() {
  console.log('🐛 Starting debug test...\n');

  const client = new AutoPatchClient({
    endpoint: 'http://localhost:5249/Autopatch'
  }, {
    onConnectionChanged: (status) => {
      console.log(`🔗 Connection status: ${status}`);
    },
    onError: (error) => {
      console.error(`❌ Error: ${error.message}`);
    },
    onDataUpdated: (typeName, operations, isInitialData) => {
      console.log(`\n🔄 ${typeName} updated (initial: ${isInitialData})`);
      console.log(`Operations (${operations.length}):`);
      operations.forEach((op, i) => {
        console.log(`  ${i}: ${JSON.stringify(op)}`);
      });
      console.log('');
    }
  });

  try {
    await client.connect();
    console.log('✅ Connected\n');

    // Subscribe with admin auth
    const result = await client.subscribeToType('PizzaOrder', null, 'admin');
    console.log(`PizzaOrder subscription: ${result.success}\n`);

    // Monitor for 10 seconds
    setTimeout(() => {
      console.log('🛑 Debug complete');
      client.disconnect();
      process.exit(0);
    }, 10000);

  } catch (error) {
    console.error(`💥 Failed: ${error.message}`);
    process.exit(1);
  }
}

debugTest();