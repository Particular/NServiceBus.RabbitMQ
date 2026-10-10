# Receiving, retrying, and dispatching messages

The delivery guarantees the transport provides, and where the code enforces them. For the user-facing contract, see [transactions and delivery guarantees](https://docs.particular.net/transports/rabbitmq/transactions-and-delivery-guarantees).

## Receiving

[`MessagePump.cs`](../src/NServiceBus.Transport.RabbitMQ/Receiving/MessagePump.cs) consumes with manual acknowledgements. It acknowledges after successful processing, and rejects with requeue when the message must be retried. The transport supports only `TransportTransactionMode.ReceiveOnly`. Why there is no auto-ack mode and no `None`: [consume with manual acknowledgements only](decisions/2016-02-29-manual-acknowledgement-only.md).

Consequences that code changes must preserve:

- **Duplicates are possible.** A connection lost after processing but before the acknowledgement leads to redelivery. Duplicate handling belongs to the outbox or idempotent handlers, not the transport.
- **One delivery is one attempt.** Immediate retries are broker redeliveries, not an in-process loop, so a single attempt stays within the broker's consumer acknowledgement timeout. The attempt number comes from `x-delivery-count` on quorum queues, or from a bounded in-memory LRU cache on classic queues. See [perform immediate retries through broker redelivery](decisions/2022-09-13-immediate-retries-through-broker-redelivery.md).
- **The broker must not delete messages that recoverability still retries.** Quorum queue delivery limits are therefore verified at startup. See [broker verification](broker-verification.md).
- **A failed acknowledgement is remembered.** When the channel closes before a successful message can be acknowledged, the message ID is cached. When the redelivery arrives, it is acknowledged without processing it again.
- **Poison messages are moved, not dropped.** A message whose headers or message ID cannot be read is sent to the error queue with the topology's raw send. If that fails, the message is requeued. Forwarding poison messages to the error queue dates from 2015 ([374f72f7](https://github.com/Particular/NServiceBus.RabbitMQ/commit/374f72f7)).
- **Purge on startup is limited to the endpoint's own queue.** It runs during initialization, before consumption starts ([`QueuePurger.cs`](../src/NServiceBus.Transport.RabbitMQ/Administration/QueuePurger.cs)).

## Dispatching

[`MessageDispatcher.cs`](../src/NServiceBus.Transport.RabbitMQ/Sending/MessageDispatcher.cs) sends all operations of a dispatch over the shared publish channel, then awaits all of their confirms together.

- **Publisher confirms are always on.** A dispatch completes only after the broker has confirmed every message. See [always use publisher confirms](decisions/2020-07-08-always-use-publisher-confirms.md).
- **`mandatory` is set for sends.** Unicast sends, raw sends of poison messages, and publishes into the delay infrastructure set `mandatory=true`, so an unroutable message is returned to the sender. Event publishes do not, because an event without subscribers is valid ([`ConventionalRoutingTopology.cs`](../src/NServiceBus.Transport.RabbitMQ/Routing/ConventionalRoutingTopology.cs), [`DirectRoutingTopology.cs`](../src/NServiceBus.Transport.RabbitMQ/Routing/DirectRoutingTopology.cs)).
- **Messages are persistent by default.** `UseNonPersistentDeliveryMode()` on send, publish, or reply options opts a single message out. The marker header is consumed when the AMQP properties are built and never reaches the wire ([0370444a](https://github.com/Particular/NServiceBus.RabbitMQ/commit/0370444ada0f144c3207351d5729a389206a0b92)).
- **Time-to-be-received maps to the AMQP `expiration` property.** It cannot be combined with a delayed message, which matches the old timeout manager's behavior ([`BasicPropertiesExtensions.cs`](../src/NServiceBus.Transport.RabbitMQ/Sending/BasicPropertiesExtensions.cs)).
- **`OutgoingNativeMessageCustomization` runs last.** It runs after the transport has filled in the AMQP properties and immediately before publishing. A callback can therefore override transport defaults, deliberately ([#1468](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1468)).

## Decisions

- [Consume with manual acknowledgements only](decisions/2016-02-29-manual-acknowledgement-only.md)
- [Always use publisher confirms](decisions/2020-07-08-always-use-publisher-confirms.md)
- [Perform immediate retries through broker redelivery](decisions/2022-09-13-immediate-retries-through-broker-redelivery.md)
- [Enforce an effectively unlimited delivery limit on quorum queues](decisions/2025-02-25-effectively-unlimited-quorum-delivery-limit.md)
