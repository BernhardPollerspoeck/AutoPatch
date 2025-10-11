import React from 'react'

interface ConnectionStatusProps {
  isConnected: boolean
  error: Error | null
}

const ConnectionStatus: React.FC<ConnectionStatusProps> = ({ isConnected, error }) => {
  if (error) {
    return (
      <div className="connection-status error">
        <span className="status-indicator">❌</span>
        <span>Connection Error: {error.message}</span>
      </div>
    )
  }

  if (isConnected) {
    return (
      <div className="connection-status connected">
        <span className="status-indicator">✅</span>
        <span>Connected to AutoPatch Server</span>
      </div>
    )
  }

  return (
    <div className="connection-status connecting">
      <span className="status-indicator">🔄</span>
      <span>Connecting to AutoPatch Server...</span>
    </div>
  )
}

export default ConnectionStatus