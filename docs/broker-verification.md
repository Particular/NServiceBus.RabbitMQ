# Broker verification and the management API

What the transport checks about the broker before it starts, and why it needs the RabbitMQ management API to do so. For configuration and permissions, see [configuring RabbitMQ management API access](https://docs.particular.net/transports/rabbitmq/connection-settings#configuring-rabbitmq-management-api-access) and [delivery limit validation](https://docs.particular.net/transports/rabbitmq/connection-settings#delivery-limit-validation).

## Why the management API

Some broker properties the transport depends on cannot be read over AMQP:

- the broker version;
- enabled feature flags;
- a queue's effective policy and delivery limit.

Since version 10 ([#1512](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1512)), the transport reads them over the management HTTP API. Delivery-limit validation prompted the change; see [enforce an effectively unlimited delivery limit](decisions/2025-02-25-effectively-unlimited-quorum-delivery-limit.md). The same client replaced the earlier AMQP-based version and feature-flag probes ([#1039](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1039), [#1041](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1041)). The command line tool reuses it.

## What is verified

[`BrokerVerifier.cs`](../src/NServiceBus.Transport.RabbitMQ/Administration/BrokerVerifier.cs) runs during transport initialization, and per queue before each message pump starts.

| Check | Why | Failure |
| --- | --- | --- |
| Broker version ≥ 3.10.0 | Quorum queues with at-least-once dead lettering for [delayed delivery](delayed-delivery.md) | Startup fails |
| `stream_queue` feature flag enabled | Required by RabbitMQ for at-least-once dead lettering | Startup fails |
| Each consumed quorum queue has no custom delivery limit | Broker-side deletion must not cut NServiceBus recoverability short | Startup fails |
| On RabbitMQ 4.0+, no delivery-limit policy applies | RabbitMQ 4.0 defaults quorum queues to 20 deliveries | The transport creates `nsb.<queue>.delivery-limit` with `100000`, or fails if another policy already applies |

Classic queues have no delivery limit and are skipped. `100000` is the value the transport treats as unlimited, because RabbitMQ does not reliably honor `-1` when it is set through a policy ([#1688](https://github.com/Particular/NServiceBus.RabbitMQ/issues/1688), [#1689](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1689)).

## Failure behavior and disabling checks

Every failed check stops the endpoint from starting, with a message that explains how to fix the broker. Some environments cannot grant management API access. For those, [#1592](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1592) added `DisableBrokerRequirementChecks` (per check) and `ValidateDeliveryLimits = false`. Both log warnings that delayed delivery or retries may lose messages. When every check is disabled, the transport does not contact the management API at all.

## Connecting to the management API

[`ManagementClient.cs`](../src/NServiceBus.Transport.RabbitMQ/Administration/ManagementApi/ManagementClient.cs) and [`ManagementApiConfiguration.cs`](../src/NServiceBus.Transport.RabbitMQ/Configuration/ManagementApiConfiguration.cs):

- By default, the client derives the URL from the AMQP host (HTTP 15672, or HTTPS 15671 when TLS is used) and reuses the AMQP credentials. Users can override the URL, the credentials, or both ([#1573](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1573)).
- The client uses relative request paths, so it preserves a base URL path prefix, for example behind a reverse proxy ([#1701](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1701), [#1712](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1712)).
- `ValidateRemoteCertificate = false` applies to the management client as well as to AMQP ([#1599](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1599)).

## Other consumers

`ManagementClient` is internal, but the transport project grants `InternalsVisibleTo` to `ServiceControl.Transports.RabbitMQ`. ServiceControl uses the client for queue length monitoring and for queue discovery and throughput in usage reports. Internal changes to the client can therefore break another repository. Check ServiceControl before changing its surface or behavior.

## Decisions

- [Enforce an effectively unlimited delivery limit on quorum queues](decisions/2025-02-25-effectively-unlimited-quorum-delivery-limit.md)
- [Build the delay infrastructure from quorum queues with at-least-once dead lettering](decisions/2022-05-17-quorum-queues-for-delay-infrastructure.md)
