import React from 'react';
import { ConnectionStatus } from '@autopatch/client';

interface ConnectionIndicatorProps {
  status: ConnectionStatus;
}

const ConnectionIndicator: React.FC<ConnectionIndicatorProps> = ({ status }) => {
  const getStatusInfo = (status: ConnectionStatus) => {
    switch (status) {
      case ConnectionStatus.Connected:
        return { text: 'Connected', icon: '✅' };
      case ConnectionStatus.Connecting:
        return { text: 'Connecting...', icon: '🔄' };
      case ConnectionStatus.Reconnecting:
        return { text: 'Reconnecting...', icon: '🔄' };
      case ConnectionStatus.Disconnecting:
        return { text: 'Disconnecting...', icon: '⏳' };
      case ConnectionStatus.Disconnected:
      default:
        return { text: 'Disconnected', icon: '❌' };
    }
  };

  const { text, icon } = getStatusInfo(status);
  const className = `connection-indicator ${status.toLowerCase()}`;

  return (
    <div className={className}>
      <span className="connection-dot"></span>
      <span>{icon} {text}</span>
    </div>
  );
};

export default ConnectionIndicator;