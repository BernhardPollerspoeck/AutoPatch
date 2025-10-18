import React, { useEffect, useState } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

const SimpleSignalRTest: React.FC = () => {
  const [connectionStatus, setConnectionStatus] = useState('Disconnected')
  const [messages, setMessages] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('http://localhost:5249/autopatch', {
        timeout: 60000 // 60 seconds timeout
      })
      .configureLogging(LogLevel.Information)
      .build()

    connection.onclose((error) => {
      console.log('[SimpleTest] Connection closed:', error)
      setConnectionStatus('Disconnected')
      if (error) {
        setError(`Connection closed: ${error.message}`)
      }
    })

    const connectAndTest = async () => {
      try {
        setConnectionStatus('Connecting...')
        setError(null)
        
        console.log('[SimpleTest] Starting connection...')
        await connection.start()
        
        console.log('[SimpleTest] Connection started! State:', connection.state)
        setConnectionStatus('Connected')
        
        // Try calling SubscribeToType
        console.log('[SimpleTest] Calling SubscribeToType...')
        const result = await connection.invoke('SubscribeToType', 'PizzaOrder', null, null)
        console.log('[SimpleTest] SubscribeToType result:', result)
        
        setMessages(prev => [...prev, `Connected successfully! SubscribeToType result: ${result}`])
        
      } catch (err) {
        console.error('[SimpleTest] Error:', err)
        setConnectionStatus('Error')
        setError(err instanceof Error ? err.message : String(err))
      }
    }

    connectAndTest()

    return () => {
      console.log('[SimpleTest] Cleaning up connection...')
      connection.stop()
    }
  }, [])

  return (
    <div style={{ padding: '20px', border: '2px solid #ccc', margin: '20px' }}>
      <h3>Simple SignalR Connection Test</h3>
      <p><strong>Status:</strong> {connectionStatus}</p>
      {error && <p style={{ color: 'red' }}><strong>Error:</strong> {error}</p>}
      <div>
        <strong>Messages:</strong>
        <ul>
          {messages.map((msg, index) => (
            <li key={index}>{msg}</li>
          ))}
        </ul>
      </div>
    </div>
  )
}

export default SimpleSignalRTest