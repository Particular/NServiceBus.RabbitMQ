# Perform immediate retries through broker redelivery

Reconstructed in 2026 from the linked pull requests, issues, and public documentation. Statements marked *Inference* are not stated directly in those sources.

## Context

RabbitMQ 3.8.15 introduced a [consumer acknowledgement timeout](https://www.rabbitmq.com/docs/consumers#acknowledgement-timeout): when a consumer does not acknowledge a delivery within the timeout (30 minutes by default since 3.8.17), the broker closes the channel and requeues the message.

- [#843](https://github.com/Particular/NServiceBus.RabbitMQ/issues/843): the message pump only reacted to connection shutdown, so a channel closed by the timeout silently stopped consumption. [#894](https://github.com/Particular/NServiceBus.RabbitMQ/pull/894) added recovery from channel and consumer failures.
- [#927](https://github.com/Particular/NServiceBus.RabbitMQ/issues/927): the transport performed NServiceBus immediate retries in a loop within a single delivery. When the handler time multiplied by the number of immediate retries exceeded the timeout, the message could no longer be acknowledged. It was redelivered indefinitely and the configured recoverability policy never applied.

The documented workaround was to raise the broker's consumer timeout, which the transport cannot enforce.

## Decision

[#1071](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1071) changed the message pump to make one processing attempt per broker delivery. When recoverability asks for an immediate retry, the delivery is rejected with requeue, and the broker redelivers it.

The attempt number is derived from the delivery in [`MessagePump.cs`](../../src/NServiceBus.Transport.RabbitMQ/Receiving/MessagePump.cs):

- the first delivery (`redelivered=false`) is attempt 1;
- from a quorum queue, the attempt is the `x-delivery-count` header plus one;
- from a classic queue, which has no delivery count, the attempt comes from a bounded in-memory LRU cache keyed by message ID and delayed-retry count.

The pump also logs a specific message when the channel was closed because the consumer timeout was exceeded. The fix was backported to 6.1.3 and 7.0.2.

## Consequences

- A single attempt only needs to finish within the consumer timeout, so recoverability policies apply again. A single handler invocation that exceeds the timeout still cannot be acknowledged; that limitation is accepted, and the mitigation remains raising the broker timeout.
- The broker's delivery count now drives NServiceBus recoverability. Since RabbitMQ 4.0, a quorum queue's default delivery limit can delete a message before recoverability finishes. [Enforce an effectively unlimited delivery limit](2025-02-25-effectively-unlimited-quorum-delivery-limit.md) describes the resolution.
- *Inference:* on classic queues, attempt counting is local to one process. It is lost on restart and not shared between competing consumers, so a message may receive more immediate retries than configured. This is accepted because quorum queues, the recommended type, carry the count with the message.
- The original 100-entry caches were later increased to 1,000 entries ([8b11ed78](https://github.com/Particular/NServiceBus.RabbitMQ/commit/8b11ed7827de8ac5129177149dae611cb87be22f)).

## Alternative approaches

- Keep the in-process retry loop and require a larger consumer timeout. This was the documented workaround in #927. It was rejected as the default because the transport cannot control broker configuration, and a misconfigured broker caused infinite redelivery.
- Count attempts only in memory for every queue type. This was the behavior implied by the old loop. #1071 rejected it because the broker's `x-delivery-count`, where available, survives requeues and moves between consumers.
