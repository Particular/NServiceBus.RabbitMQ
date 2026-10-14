# Delayed delivery

How the transport delays messages inside the broker, and which constraints the design must keep. For configuration and operator guidance, see the public [delayed delivery](https://docs.particular.net/transports/rabbitmq/delayed-delivery) documentation.

## Why the broker does the delaying

Delayed sends, delayed retries, and saga timeouts all need delayed delivery. Since version 4.3 ([#329](https://github.com/Particular/NServiceBus.RabbitMQ/pull/329)), the transport implements it with standard RabbitMQ features (message TTL and dead-letter exchanges) instead of the NServiceBus timeout manager and its persistence. The timeout manager remained an opt-in migration path for already stored timeouts ([#469](https://github.com/Particular/NServiceBus.RabbitMQ/pull/469)) until NServiceBus core removed it. Saga timeouts and delayed retries are unavailable when delayed delivery is disabled ([#1422](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1422), 9.1).

## The level chain

[`DelayInfrastructure.cs`](../src/NServiceBus.Transport.RabbitMQ/DelayedDelivery/DelayInfrastructure.cs) declares 28 levels, numbered 27 down to 0. Each level is a topic exchange `nsb.v2.delay-level-NN` with a queue of the same name whose message TTL is 2^N seconds. Every level dead-letters into the next lower level, and level 0 dead-letters into the `nsb.v2.delay-delivery` exchange.

- The routing key is the delay in seconds written as 28 binary digits, highest bit first, separated by dots and followed by the destination address. For example, `0.0. … 1.0.1.Sales`.
- The transport publishes the message to the level of the highest set bit.
- At each level, the bindings route the message either into that level's queue, where it waits 2^N seconds (bit set), or directly to the next level's exchange (bit clear).
- At `nsb.v2.delay-delivery`, the binding `#.<address>` that every receiving endpoint creates routes the message to its destination.

The maximum delay is 2^28 − 1 seconds, about 8.5 years. Longer delays are rejected when the message is sent ([`BasicPropertiesExtensions.cs`](../src/NServiceBus.Transport.RabbitMQ/Sending/BasicPropertiesExtensions.cs)).

## Invariants

- The routing key has a fixed width. Destination addresses may contain dots, so tooling must parse the first 28 segments as delay bits and treat the rest as the address, never "everything after the last dot" ([#1609](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1609)).
- Delayed messages are dead-lettered at least once. The level queues are quorum queues with `x-dead-letter-strategy=at-least-once` and `x-overflow=reject-publish`. This requires RabbitMQ 3.10 or later and the `stream_queue` feature flag, which the transport verifies at startup. [Build the delay infrastructure from quorum queues](decisions/2022-05-17-quorum-queues-for-delay-infrastructure.md) explains why.
- The sender binds the destination before every delayed send. [`ConfirmsAwareChannel.SendMessage`](../src/NServiceBus.Transport.RabbitMQ/Connection/ConfirmsAwareChannel.cs) calls `IRoutingTopology.BindToDelayInfrastructure` and then publishes with `mandatory=true`. A destination that does not exist yet therefore fails at the sender instead of losing the message later. The `AllEndpointsSupportDelayedDelivery` optimization that skipped this binding was removed in 4.3.1 because of that loss ([#367](https://github.com/Particular/NServiceBus.RabbitMQ/issues/367), [#368](https://github.com/Particular/NServiceBus.RabbitMQ/pull/368)).
- Infrastructure headers are removed on receive. [`MessageConverter.cs`](../src/NServiceBus.Transport.RabbitMQ/Receiving/MessageConverter.cs) strips the delay header and the `x-death` and `x-first-death-*` headers, so forwarding a message does not carry stale delay state.
- The version is part of the entity names. A layout change that is not backward compatible gets new names, so the old and new infrastructure can coexist during an upgrade. "v1" (`nsb.delay-level-NN`, classic queues) is never modified by the current transport.

## Lifecycle and tooling

- Endpoints with installers enabled create the infrastructure in `RabbitMQTransportInfrastructure.SetupInfrastructure`, unless delayed delivery is disabled. In that case, delayed sends throw ([`MessageDispatcher.cs`](../src/NServiceBus.Transport.RabbitMQ/Sending/MessageDispatcher.cs)).
- The `rabbitmq-transport delays` commands ([`Commands/Delays`](../src/NServiceBus.Transport.RabbitMQ.CommandLine/Commands/Delays/)) are documented in [operations scripting](https://docs.particular.net/transports/rabbitmq/operations-scripting):
  - `create` and `verify` provision and check the infrastructure and broker requirements.
  - `migrate` moves in-flight messages from v1 to v2 ([#990](https://github.com/Particular/NServiceBus.RabbitMQ/pull/990)).
  - `transfer` moves delayed messages to another broker, for example during a blue-green broker migration ([#1765](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1765)).

  `migrate` and `transfer` recalculate the remaining delay from the message headers, and move messages they cannot process to a poison queue.

## Decisions

- [Build the delay infrastructure from quorum queues with at-least-once dead lettering](decisions/2022-05-17-quorum-queues-for-delay-infrastructure.md)
- Routing-key encoding is performance-sensitive: [#1411](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1411) and [#1617](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1617)
