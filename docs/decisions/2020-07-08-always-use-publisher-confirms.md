# Always use publisher confirms

Reconstructed in 2026 from the linked pull request, issue, and public documentation. Statements marked *Inference* are not stated directly in those sources.

## Context

From the 4.x era until version 6, publisher confirms were optional (`UsePublisherConfirms`). [#135](https://github.com/Particular/NServiceBus.RabbitMQ/issues/135) analyzed what happens when an endpoint sends to a destination that does not exist yet:

| | Confirms enabled | Confirms disabled |
| --- | --- | --- |
| Conventional topology | The first bad message fails with a channel-closed exception | Message loss when few messages are sent, or a later `AlreadyClosedException` |
| Direct topology | "Message could not be routed" for the first bad message | Only a warning log entry |

With confirms disabled, a send could complete before the broker had rejected it, and the dispatch had no way to report the failure. #135 was originally closed as something the transport could only warn about.

## Decision

[#651](https://github.com/Particular/NServiceBus.RabbitMQ/pull/651) (merged in commit [5eea0090](https://github.com/Particular/NServiceBus.RabbitMQ/commit/5eea0090b25103a6b467f978593db3cb89335f61), released in version 6) removed `UsePublisherConfirms` and the related settings. Every publishing channel uses publisher confirms. In the pull request's words: "We generally don't think anyone should disable publisher confirms when using NServiceBus."

The transport creates every publish channel with confirmation tracking ([`ConfirmsAwareChannel.cs`](../../src/NServiceBus.Transport.RabbitMQ/Connection/ConfirmsAwareChannel.cs)). [`MessageDispatcher.cs`](../../src/NServiceBus.Transport.RabbitMQ/Sending/MessageDispatcher.cs) completes a dispatch only after the broker confirms every operation. Unicast sends and sends to the delay infrastructure also set the AMQP `mandatory` flag, so an unroutable message is returned instead of being silently dropped. Publishes do not set it, because an event with no subscribers is not an error.

## Consequences

- A dispatch fails when the broker rejects a message or the channel closes, so the outbox, recoverability, or the caller sees the failure instead of losing the message.
- Every dispatch waits for broker confirms, which adds latency. This is mitigated by dispatching all operations of a batch concurrently and awaiting them together. The throughput cost was accepted without a recorded measurement.
- The [5 to 6 upgrade guide](https://docs.particular.net/transports/upgrades/rabbitmq-5to6) documents the removal. Users who had disabled confirms for throughput lost that option.
- Confirms make each send reliable but do not make sends atomic with the receive acknowledgement. See [consume with manual acknowledgements only](2016-02-29-manual-acknowledgement-only.md).

## Alternative approaches

- Keep confirms optional with a warning. This was the state before version 6. #651 rejected it because a configuration that can lose messages silently is not one NServiceBus users should be able to choose.
- Detect unroutable messages without confirms, using the `mandatory` flag or an [alternate exchange](https://www.rabbitmq.com/docs/ae). #135 rejected both as complete solutions. With the conventional topology, the destination exchange does not exist, so the broker closes the channel instead of returning the message, and an alternate exchange cannot be attached to an exchange that does not exist.

## Open questions

- No measurement of the throughput cost of mandatory confirms was found. Record one before proposing an opt-out again.
