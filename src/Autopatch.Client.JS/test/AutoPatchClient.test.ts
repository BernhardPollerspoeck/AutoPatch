import { afterEach, describe, expect, it, vi } from 'vitest';

vi.mock('@microsoft/signalr', () => import('./fake-signalr'));

import { AutoPatchClient } from '../src/AutoPatchClient';
import { builds } from './fake-signalr';

const itemA = { id: 1, name: 'a' };
const itemB = { id: 2, name: 'b' };
const add = (value: unknown) => ({ op: 'add', path: '/-', value });

async function connectedClient(config: Record<string, unknown> = {}) {
  const client = new AutoPatchClient({ endpoint: 'http://localhost/autopatch', ...config });
  const build = builds[builds.length - 1];
  await client.connect();
  return { client, connection: build.connection, build };
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('J1 resubscribe after reconnect', () => {
  it('J1: subscribes again on the server after an automatic reconnect', async () => {
    const { client, connection } = await connectedClient();
    await client.subscribeToType('TestItem');

    await connection.simulateReconnect();

    expect(
      connection.subscribeCalls(),
      'resubscribeAll calls subscribeToType, which returns early because the key is still in this.subscriptions, so the new connection never joins the group again',
    ).toHaveLength(2);
  });

  it('J1: keeps reconnecting instead of giving up after four attempts', async () => {
    const { build } = await connectedClient();
    const policy = build.reconnectPolicy;

    const delayAfterManyAttempts = Array.isArray(policy)
      ? policy[10] ?? null
      : policy?.nextRetryDelayInMilliseconds?.({ previousRetryCount: 10, elapsedMilliseconds: 600_000 });

    expect(
      delayAfterManyAttempts,
      `withAutomaticReconnect(${JSON.stringify(policy)}) stops after the last delay and the client stays disconnected`,
    ).not.toBeNull();
  });
});

describe('J2 initial set and resubscribe', () => {
  it('J2: a second initial set replaces the items instead of appending them', async () => {
    const { client, connection } = await connectedClient();
    await client.subscribeToType('TestItem');
    connection.deliver('AutoPatch/TestItem', [add(itemA)], true);

    connection.deliver('AutoPatch/TestItem', [add(itemA)], true);

    expect(client.getCollection('TestItem'), 'full data is applied with applyPatch on top of the existing items').toEqual([itemA]);
  });

  it('J2: patches that arrive before the initial set are ignored', async () => {
    const { client, connection } = await connectedClient();
    await client.subscribeToType('TestItem');

    connection.deliver('AutoPatch/TestItem', [add(itemB)], false);
    connection.deliver('AutoPatch/TestItem', [add(itemA)], true);

    expect(
      client.getCollection('TestItem'),
      'unlike the .NET client, patches before the initial set are applied, so the initial set is appended to them',
    ).toEqual([itemA]);
  });

  it('J2: resubscribing keeps the auth string that was passed to subscribeToType', async () => {
    const { client, connection } = await connectedClient();
    await client.subscribeToType('TestItem', undefined, 'secret-auth');
    const resubscribe = vi.spyOn(client, 'subscribeToType');

    await connection.simulateReconnect();

    expect(
      connection.subscribeCalls()[1]?.args[2],
      `resubscribeAll calls subscribeToType with ${JSON.stringify(resubscribe.mock.calls)} - the per-call authString is lost`,
    ).toBe('secret-auth');
  });

  it('J2: resubscribing keeps collection keys that contain a slash', async () => {
    const { client, connection } = await connectedClient();
    await client.subscribeToType('TestItem', 'tenant/a');
    const resubscribe = vi.spyOn(client, 'subscribeToType');

    await connection.simulateReconnect();

    expect(
      connection.subscribeCalls()[1]?.args[1],
      `resubscribeAll calls subscribeToType with ${JSON.stringify(resubscribe.mock.calls)} - split('/', 2) cuts the key off`,
    ).toBe('tenant/a');
  });
});

describe('C3 repeated subscriptions (JS client)', () => {
  it('C3: validates a repeated subscription with its own credentials', async () => {
    const { client, connection } = await connectedClient();
    await client.subscribeToType('TestItem', undefined, 'valid');
    connection.invoke = async <T>(method: string, ...args: any[]): Promise<T> => {
      connection.invocations.push({ method, args });
      return (args[2] === 'valid') as T;
    };

    const second = await client.subscribeToType('TestItem', undefined, 'invalid');

    expect(connection.subscribeCalls(), 'a repeated subscription must ask the server').toHaveLength(2);
    expect(second.success).toBe(false);
  });
});

describe('C4 access token (JS client)', () => {
  it('C4: passes an access token factory to SignalR', async () => {
    const accessTokenFactory = () => 'token';
    const { build } = await connectedClient({ accessTokenFactory });

    expect(
      build.options?.accessTokenFactory,
      'withUrl only receives { timeout }, so bearer tokens cannot be sent (only cookies work in the browser)',
    ).toBe(accessTokenFactory);
  });
});

describe('J3 logging', () => {
  it('J3: does not write to console.log during normal operation', async () => {
    const log = vi.spyOn(console, 'log').mockImplementation(() => {});

    const { client, connection } = await connectedClient();
    await client.subscribeToType('TestItem');
    connection.deliver('AutoPatch/TestItem', [add(itemA)], true);

    expect(log.mock.calls.length, `console.log was called with: ${JSON.stringify(log.mock.calls.slice(0, 3))}`).toBe(0);
  });
});
