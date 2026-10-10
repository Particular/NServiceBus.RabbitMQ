# Consume with manual acknowledgements only

Reconstructed in 2026 from the linked issues, commits, and public documentation. Statements marked *Inference* are not stated directly in those sources.

## Context

Before 2016, an endpoint running with transport transactions disabled (`TransportTransactionMode.None`) consumed with RabbitMQ's automatic acknowledgement mode (`noAck=true`). [#133](https://github.com/Particular/NServiceBus.RabbitMQ/issues/133) documented two failures of that mode:

- RabbitMQ cannot rate-limit deliveries to an auto-ack consumer, because [consumer prefetch](https://www.rabbitmq.com/docs/consumer-prefetch) only bounds unacknowledged deliveries. When messages arrive faster than the endpoint processes them, the client buffers them in memory until an `OutOfMemoryException` is thrown.
- The broker considers a delivered message acknowledged immediately, so every message still in the client buffer is lost when the endpoint stops.

Neither failure is visible from configuration: the user only chose "no transactions".

## Decision

Every consumer uses manual acknowledgements, regardless of the configured transaction mode:

- [05a54fed](https://github.com/Particular/NServiceBus.RabbitMQ/commit/05a54fed684f4ada2d647bdfe3c77fecd68743e9) (2016-01-26) removed the ability to consume with `noAck=true`.
- [bb9c8bd7](https://github.com/Particular/NServiceBus.RabbitMQ/commit/bb9c8bd74dae0927fcf6e3a38965bbd141e230e2) (2016-02-29) stopped using auto-ack when transactions are disabled, closing #133.
- A message is acknowledged after it is processed successfully, and rejected with requeue when it must be retried. See [`MessagePump.cs`](../../src/NServiceBus.Transport.RabbitMQ/Receiving/MessagePump.cs).

`None` therefore no longer behaved differently from `ReceiveOnly`, but before NServiceBus 8 a transport had no way to express that. Once NServiceBus 8 made it possible, [06a3f192](https://github.com/Particular/NServiceBus.RabbitMQ/commit/06a3f1925b354211a6140a8f068550fd2d06e62e) (2022-06-06, "Stop lying about what transaction modes we support") made the transport advertise only `ReceiveOnly` from version 8 onwards. The [7 to 8 upgrade guide](https://docs.particular.net/transports/upgrades/rabbitmq-7to8) documents the change.

## Consequences

- Delivery is at-least-once. If the connection is lost after a handler completes but before the acknowledgement reaches the broker, the message is redelivered and processed again. This is accepted and documented in [transactions and delivery guarantees](https://docs.particular.net/transports/rabbitmq/transactions-and-delivery-guarantees); the mitigation is the outbox or idempotent handlers.
- Memory is bounded by the prefetch count, which the transport derives from the endpoint's concurrency. See [connections and channels](../connections-and-channels.md).
- Between 2016 and version 8, an endpoint configured with `None` silently received `ReceiveOnly` behavior. That mismatch was accepted until version 8 corrected what the transport advertises.
- A handler that runs longer than the broker's consumer acknowledgement timeout cannot acknowledge its delivery. This interaction is addressed by [immediate retries through broker redelivery](2022-09-13-immediate-retries-through-broker-redelivery.md).

## Alternative approaches

- **Keep auto-ack for `None` as a throughput option.** Rejected in #133: the memory growth is unbounded, and messages are lost on every endpoint stop, not only on failure.
- **Keep advertising `None` and document that it behaves like `ReceiveOnly`.** This was the de facto state from 2016 to version 8, while NServiceBus core offered no alternative. It was dropped in 06a3f192 because it misrepresented the transport's guarantees.

## Open questions

- No public record shows whether AMQP channel transactions (`tx.select`) were evaluated as a way to offer `SendsAtomicWithReceive`. Ask the maintainers before proposing it.
