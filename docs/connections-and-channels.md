# Connections, channels, and concurrency

How the transport uses AMQP connections and channels, how it recovers from failures, and how concurrency maps to prefetch. For settings, see [connection settings](https://docs.particular.net/transports/rabbitmq/connection-settings).

## Connection roles

[`ConnectionFactory.cs`](../src/NServiceBus.Transport.RabbitMQ/Connection/ConnectionFactory.cs) creates every connection. Connections are named after the endpoint, so operators can identify them in the management UI ([#563](https://github.com/Particular/NServiceBus.RabbitMQ/pull/563)).

| Connection | Owner | Channels |
| --- | --- | --- |
| One per message pump (receive queue) | [`MessagePump.cs`](../src/NServiceBus.Transport.RabbitMQ/Receiving/MessagePump.cs) | One consuming channel without publisher confirms, with `BasicQos` and consumer dispatch concurrency set to the endpoint's maximum concurrency |
| `<endpoint> Publish`, one per endpoint | [`ChannelProvider.cs`](../src/NServiceBus.Transport.RabbitMQ/Connection/ChannelProvider.cs) | One shared, confirm-tracking publish channel |
| `<endpoint> Administration`, short-lived | Installers, subscription manager, purge | One channel for each operation |

**Separate receive connections.** Each message pump owns its connection, so consumer failures, concurrency changes, and recovery do not interfere with publishing.

**One shared publish channel.** Before version 10, the transport kept a pool of publish channels. The synchronous RabbitMQ.Client required that a channel not be used concurrently, but in practice the pool rarely needed more than one or two channels. With the asynchronous RabbitMQ.Client 7, concurrent dispatches made the pool create a channel per message and exhaust the broker's `channel_max` ([#1621](https://github.com/Particular/NServiceBus.RabbitMQ/issues/1621)). Since [#1620](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1620), the provider lazily creates one channel and shares it. It replaces the channel under a semaphore when the channel closes. Client 7 channels are safe for concurrent publishing as long as they are not also used for consuming.

## Recovery

- **Receive.** The pump reacts to three events: an unexpected connection shutdown, an unexpected channel shutdown (for example after a consumer acknowledgement timeout), and the broker cancelling the consumer. Each one arms the circuit breaker and starts a reconnect loop that retries every `NetworkRecoveryInterval` (default 10 seconds). Recovery from channel and consumer failures was added because those failures used to stop consumption silently ([#843](https://github.com/Particular/NServiceBus.RabbitMQ/issues/843), [#894](https://github.com/Particular/NServiceBus.RabbitMQ/pull/894)). The reconnect loop disposes the old connection first, to prevent leaks and races ([#1109](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1109), [#1435](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1435)).
- **Circuit breaker.** [`MessagePumpConnectionFailedCircuitBreaker.cs`](../src/NServiceBus.Transport.RabbitMQ/Receiving/MessagePumpConnectionFailedCircuitBreaker.cs) raises the critical error action when the pump stays disconnected for `TimeToWaitBeforeTriggeringCircuitBreaker` (default 2 minutes). A successful consumer registration disarms it.
- **Publish.** `ChannelProvider` listens for connection shutdown and replaces the publish connection on the same interval.
- **Heartbeats.** `HeartbeatInterval` (default 60 seconds) controls how quickly a dead TCP connection is detected.
- **Concurrency changes** stop the pump gracefully: in-flight messages are allowed to finish before the pump restarts. Aborting the connection would have made their acknowledgements fail ([#1258](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1258)).

## Prefetch and concurrency

`PrefetchCountCalculation` receives the endpoint's maximum concurrency and defaults to `3 * concurrency` ([e416a15a](https://github.com/Particular/NServiceBus.RabbitMQ/commit/e416a15acd45796dcc69e79e8dedbd1c856cea73)). A result below the concurrency is raised to the concurrency with a warning, and the value is capped at `ushort.MaxValue`. Prefetch only bounds unacknowledged deliveries, which is one reason the transport never uses auto-ack (see [consume with manual acknowledgements only](decisions/2016-02-29-manual-acknowledgement-only.md)).

## Hosts, clusters, and security

- **One host per connection string.** A connection string names one host. Additional cluster nodes are added with `AddClusterNode`, each with its own port and TLS setting, and are passed to the client as endpoint candidates. Connection strings with multiple hosts, `requestedHeartbeat`, `retryDelay`, or `certPath` are rejected with a message pointing to the replacement API ([`ConnectionConfiguration.cs`](../src/NServiceBus.Transport.RabbitMQ/Configuration/ConnectionConfiguration.cs)).
- **TLS.** TLS is enabled with `amqps://` or `useTls=true`, and defaults to port 5671. Client certificates are configured in code with `ClientCertificate` or `SetClientCertificate`, not in the connection string (version 8).
- **Certificate validation.** `ValidateRemoteCertificate = false` accepts any broker certificate, for both AMQP and the management API.
- **Authentication mechanisms.** `AuthMechanisms` accepts any RabbitMQ.Client `IAuthMechanismFactory` and takes precedence over the obsolete `UseExternalAuthMechanism` ([#1742](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1742)).

## Decisions

- Replace the per-operation publish channel pool with one shared channel — [#1620](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1620)
- Recover the receive consumer from channel and consumer failures, not only connection loss — [#894](https://github.com/Particular/NServiceBus.RabbitMQ/pull/894)
- Change concurrency by stopping receive gracefully — [#1258](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1258)
