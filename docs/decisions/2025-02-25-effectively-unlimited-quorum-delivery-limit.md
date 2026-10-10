# Enforce an effectively unlimited delivery limit on quorum queues

Reconstructed in 2026 from the linked pull requests, issues, and public documentation. Statements marked *Inference* are not stated directly in those sources.

## Context

Quorum queues have a [delivery limit](https://www.rabbitmq.com/docs/quorum-queues#poison-message-handling). When a message has been delivered more times than the limit, the broker drops it, or dead-letters it if a dead-letter exchange is configured. RabbitMQ 4.0 changed the default limit from unlimited to 20.

The transport performs immediate retries through broker redelivery ([decision record](2022-09-13-immediate-retries-through-broker-redelivery.md)). An endpoint whose recoverability allows more deliveries than the limit would therefore lose messages: the broker deletes them before delayed retries run or the message reaches the error queue ([#1550](https://github.com/Particular/NServiceBus.RabbitMQ/issues/1550)).

A queue's delivery limit comes either from the `x-delivery-limit` argument when the queue is declared, or from a policy. Existing endpoint queues were already declared, and RabbitMQ applies only one policy to a queue: the matching policy with the highest priority.

## Decision

[#1512](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1512) (version 10) made the transport verify the delivery limit of every quorum queue it consumes from before it starts receiving. The verifier ([`BrokerVerifier.cs`](../../src/NServiceBus.Transport.RabbitMQ/Administration/BrokerVerifier.cs)):

- Reads each queue's arguments and effective policy through the RabbitMQ management API. Classic queues are skipped because they have no delivery limit.
- Fails startup when a delivery limit has been set by a queue argument or by a user or operator policy.
- On RabbitMQ 4.0 and later, creates a queue-specific transport policy (`nsb.<queue>.delivery-limit`, priority 0, applied to quorum queues) when no other policy applies. If another policy already applies, the transport does not override it and fails startup instead.

The change made the management API a prerequisite. The same client checks the minimum broker version and the `stream_queue` feature flag.

The policy originally set the limit to `-1` (unlimited). A support case reported message loss after a broker upgrade. [#1688](https://github.com/Particular/NServiceBus.RabbitMQ/issues/1688) found that RabbitMQ's documentation now says `-1` cannot be set through a policy. [#1689](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1689) changed the value to `100000`, which the transport treats as unlimited, and replaces its own earlier `-1` policies.

The public contract is documented in [delivery limit validation](https://docs.particular.net/transports/rabbitmq/connection-settings#delivery-limit-validation) and the [9 to 10 upgrade guide](https://docs.particular.net/transports/upgrades/rabbitmq-9to10).

## Consequences

- The transport fails closed: an endpoint does not start while a configuration could silently delete messages. Startup now depends on management API availability. That dependency is accepted. For restricted environments, [#1592](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1592) (10.1) added `DisableBrokerRequirementChecks` and `DoNotValidateDeliveryLimits`, both documented as unsafe and logged as warnings.
- Deployments need the management plugin, a reachable management endpoint, and credentials. Creating the policy needs `policymaker` permissions. Administrators can mitigate this by creating the policies in advance or by scripting validation with `rabbitmq-transport queue validate-delivery-limit`.
- When an organization already applies its own policy to endpoint queues, the transport cannot add a second one. Operators must add `"delivery-limit": 100000` to their policy. This is documented.
- `100000` is finite. *Inference:* accepted because no realistic recoverability configuration delivers a message that often.
- Startup can wait for the policy to take effect: the verifier polls the queue details up to 20 times, 3 seconds apart.

## Alternative approaches

- Rely on documentation and let operators configure delivery limits. It remains available as the "create policies in advance" path. It was rejected as the default because the failure mode is silent message loss after a broker upgrade the endpoint does not control.
- Use RabbitMQ's `-1` (unlimited) through a policy. This was the original implementation. #1689 superseded it once `-1` turned out not to be reliably honored when set through a policy.
- Declare queues with `x-delivery-limit` as a queue argument. *Inference:* only applies to queues created after the change, because queue arguments cannot be changed on an existing queue. No public record shows that it was evaluated.

## Open questions

- Which RabbitMQ versions or configurations ignored the `-1` policy? #1688 could not reproduce the loss locally.
- Was mapping the broker delivery limit into NServiceBus recoverability, instead of neutralizing it, ever considered?
