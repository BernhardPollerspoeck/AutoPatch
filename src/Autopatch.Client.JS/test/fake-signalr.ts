/**
 * In-memory stand-in for '@microsoft/signalr' that records what the AutoPatch client does with the connection.
 */

export const HubConnectionState = {
  Disconnected: 'Disconnected',
  Connecting: 'Connecting',
  Connected: 'Connected',
  Disconnecting: 'Disconnecting',
  Reconnecting: 'Reconnecting',
} as const;

export const LogLevel = {
  Trace: 0,
  Debug: 1,
  Information: 2,
  Warning: 3,
  Error: 4,
  Critical: 5,
  None: 6,
} as const;

type Handler = (...args: any[]) => void;

export class FakeHubConnection {
  state: string = HubConnectionState.Disconnected;
  readonly handlers = new Map<string, Handler[]>();
  readonly invocations: Array<{ method: string; args: any[] }> = [];
  private readonly reconnectedCallbacks: Array<(connectionId?: string) => void> = [];

  async start(): Promise<void> {
    this.state = HubConnectionState.Connected;
  }

  async stop(): Promise<void> {
    this.state = HubConnectionState.Disconnected;
  }

  on(methodName: string, handler: Handler): void {
    // The real client matches method names case-insensitively.
    const key = methodName.toLowerCase();
    this.handlers.set(key, [...(this.handlers.get(key) ?? []), handler]);
  }

  off(methodName: string): void {
    this.handlers.delete(methodName.toLowerCase());
  }

  async invoke<T>(method: string, ...args: any[]): Promise<T> {
    this.invocations.push({ method, args });
    return true as T;
  }

  onclose(): void {}

  onreconnecting(): void {}

  onreconnected(callback: (connectionId?: string) => void): void {
    this.reconnectedCallbacks.push(callback);
  }

  /** Simulates the server sending a batch to this connection. */
  deliver(methodName: string, operations: unknown[], isInitialSet: boolean): void {
    for (const handler of this.handlers.get(methodName.toLowerCase()) ?? []) {
      handler(methodName, operations, isInitialSet);
    }
  }

  /** Simulates a successful automatic reconnect; the server assigns a new connection id and forgets all groups. */
  async simulateReconnect(): Promise<void> {
    this.state = HubConnectionState.Connected;
    for (const callback of this.reconnectedCallbacks) {
      callback('new-connection-id');
    }
    await new Promise((resolve) => setTimeout(resolve, 0));
  }

  subscribeCalls(): Array<{ method: string; args: any[] }> {
    return this.invocations.filter((i) => i.method === 'SubscribeToType');
  }
}

export interface Build {
  url: string;
  options: any;
  reconnectPolicy: any;
  connection: FakeHubConnection;
}

export const builds: Build[] = [];

export class HubConnectionBuilder {
  private url = '';
  private options: any;
  private reconnectPolicy: any;

  withUrl(url: string, options?: any): this {
    this.url = url;
    this.options = options;
    return this;
  }

  withAutomaticReconnect(policy?: any): this {
    this.reconnectPolicy = policy;
    return this;
  }

  configureLogging(): this {
    return this;
  }

  build(): FakeHubConnection {
    const connection = new FakeHubConnection();
    builds.push({ url: this.url, options: this.options, reconnectPolicy: this.reconnectPolicy, connection });
    return connection;
  }
}
