# Message conversion and native integration

How AMQP messages map to NServiceBus messages in both directions, and the rules that let non-NServiceBus systems interoperate. For user guidance, see [native integration](https://docs.particular.net/transports/rabbitmq/native-integration).

## Incoming messages

[`MessageConverter.cs`](../src/NServiceBus.Transport.RabbitMQ/Receiving/MessageConverter.cs) handles incoming messages.

### Message ID

- The default strategy requires a non-empty AMQP `message-id` and fails otherwise. The message is then moved to the error queue as a poison message. The transport never invents an ID, because retries, deduplication, and the outbox depend on it.
- `MessageIdStrategy` lets an integrator derive the ID from other parts of the message, for example a header, when the sender cannot be changed (2015, [23be8aee](https://github.com/Particular/NServiceBus.RabbitMQ/commit/23be8aeee9e33d1ad4956b200e34d0c1e6888970)). The strategy must be deterministic. When it returns an empty value, the `NServiceBus.MessageId` header is used ([ff9a0dd9](https://github.com/Particular/NServiceBus.RabbitMQ/commit/ff9a0dd9fcbd0a13e2b9a3333d1795b4224987c8)).

### Header values

NServiceBus headers are strings, but AMQP header values are typed. Values are converted to strings:

- byte arrays as UTF-8;
- tables as comma-separated `key=value` pairs;
- arrays as semicolon-separated values;
- AMQP timestamps in the NServiceBus wire date format;
- everything else with `ToString()`.

The type information is lost. A system that needs exact AMQP types should read the native message, which is available in the message processing context as `BasicDeliverEventArgs`.

### AMQP properties mapped to headers

| AMQP property | NServiceBus header | Rule |
| --- | --- | --- |
| `reply-to` | `NServiceBus.ReplyToAddress` | The legacy `NServiceBus.RabbitMQ.CallbackQueue` header, when present, overrides it |
| `correlation-id` | `NServiceBus.CorrelationId` | |
| `type` | `NServiceBus.EnclosedMessageTypes` | Only when that header is absent; native senders only need to set `type` to the message type's full name |
| `content-type` | `NServiceBus.ContentType` | Added in 10.1.4, 9.2.2, and 8.0.9 ([#1666](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1666)) |
| `delivery-mode` = transient | non-persistent marker | Retries of the message keep it non-persistent |

### Headers that are removed

The delay-infrastructure headers (`NServiceBus.Transport.RabbitMQ.DelayInSeconds`, `x-death`, `x-first-death-*`), the confirmation and publish-sequence headers, and `x-delivery-count` are removed. A forwarded or audited message therefore does not carry stale broker state.

## Outgoing messages

[`BasicPropertiesExtensions.cs`](../src/NServiceBus.Transport.RabbitMQ/Sending/BasicPropertiesExtensions.cs) fills in the AMQP properties:

- the message ID;
- persistence (see [message processing](message-processing.md));
- `expiration`, from time-to-be-received;
- `type`, the first entry of `EnclosedMessageTypes`;
- `content-type`, defaulting to `application/octet-stream`;
- `reply-to` and `correlation-id`;
- all NServiceBus headers, as string values.

`OutgoingNativeMessageCustomization` (version 9 onwards, [#1468](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1468)) runs after that, immediately before publishing. It can set properties the transport does not expose, or override its defaults when a receiver expects a specific format.

## Decisions

- Make the message ID strategy pluggable rather than generating IDs — [23be8aee](https://github.com/Particular/NServiceBus.RabbitMQ/commit/23be8aeee9e33d1ad4956b200e34d0c1e6888970), [ff9a0dd9](https://github.com/Particular/NServiceBus.RabbitMQ/commit/ff9a0dd9fcbd0a13e2b9a3333d1795b4224987c8)
- Strip infrastructure headers so they are not forwarded — [4ef0493e](https://github.com/Particular/NServiceBus.RabbitMQ/commit/4ef0493efddd09b6fd465d2bf51ee799ea40baea)
- Allow last-step customization of outgoing native messages — [#1468](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1468)
