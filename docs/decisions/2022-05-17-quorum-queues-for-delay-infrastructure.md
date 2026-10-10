# Build the delay infrastructure from quorum queues with at-least-once dead lettering

Reconstructed in 2026 from the linked pull requests, issue, commits, and public documentation. Statements marked *Inference* are not stated directly in those sources.

## Context

Since version 4.3 ([#329](https://github.com/Particular/NServiceBus.RabbitMQ/pull/329), first implemented in [0e284d01](https://github.com/Particular/NServiceBus.RabbitMQ/commit/0e284d01aa4d73ff3cbc5288503a1a9ff1479844)), the transport has implemented delayed delivery inside the broker. It uses a chain of 28 levels of queues with a message TTL, connected by dead-letter exchanges. See [delayed delivery](../delayed-delivery.md) for the mechanism. A delayed message is dead-lettered once for every set bit in its delay, so every delayed send, delayed retry, and saga timeout depends on dead lettering being reliable.

The original ("v1") infrastructure used classic queues. RabbitMQ dead lettering from classic queues is [at-most-once](https://www.rabbitmq.com/docs/dlx#safety). [#1034](https://github.com/Particular/NServiceBus.RabbitMQ/issues/1034) reported the result: with mirrored classic queues, delayed messages are lost when a network partition occurs. That affects every user running the delay infrastructure in a cluster.

RabbitMQ 3.10 added [at-least-once dead lettering](https://www.rabbitmq.com/docs/quorum-queues#activating-at-least-once-dead-lettering) for quorum queues. It requires the `reject-publish` overflow strategy and the `stream_queue` feature flag.

## Decision

[#992](https://github.com/Particular/NServiceBus.RabbitMQ/pull/992) (version 7) introduced a "v2" delay infrastructure (`nsb.v2.delay-level-NN` and `nsb.v2.delay-delivery`). Its level queues are quorum queues declared with `x-dead-letter-strategy=at-least-once` and `x-overflow=reject-publish`. See [`DelayInfrastructure.cs`](../../src/NServiceBus.Transport.RabbitMQ/DelayedDelivery/DelayInfrastructure.cs).

- The transport leaves the v1 infrastructure untouched, so both can coexist. Messages already waiting in v1 are not migrated automatically. Operators move them with `rabbitmq-transport delays migrate`, added in [#990](https://github.com/Particular/NServiceBus.RabbitMQ/pull/990).
- The minimum broker version became 3.10.0, and the transport verifies the version and the `stream_queue` feature flag before it uses the infrastructure ([#1039](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1039), [#1041](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1041)).
- The same pull request removed the prerelease `RabbitMQClusterTransport` design, which never shipped in a stable release. That design made users choose between disabling delayed delivery and an explicitly unsafe classic-queue mode in clusters.

The [6 to 7 upgrade guide](https://docs.particular.net/transports/upgrades/rabbitmq-6to7) is the public record of the user-visible change.

## Consequences

- Delayed messages survive node failures and partitions, provided the broker meets the requirements. The transport enforces the requirements at startup. Users can disable the checks through `BrokerRequirementChecks`, which logs a warning that delayed delivery may not work.
- Brokers older than 3.10, or without the required feature flags, are no longer supported. This was accepted as part of the version 7 major release.
- Upgrading requires an operator action for in-flight v1 messages. Mitigations: the two versions can coexist, the migration command recalculates each message's remaining delay from its headers, and messages it cannot process are moved to a poison queue instead of being dropped.
- RabbitMQ documents that at-least-once dead lettering uses more memory and CPU. This cost was accepted without a recorded measurement for the delay levels.
- The topology is large (28 exchanges and queues) and only the transport's own tooling understands it. Moving delayed messages between brokers therefore needs `delays transfer` ([#1765](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1765)) rather than a generic shovel.

## Alternative approaches

- Keep classic queues and document the risk. This was the state before version 7, with a warning in the documentation. It was rejected because #1034 showed the loss affects every clustered user of delayed sends and delayed retries.
- Use a cluster-specific transport (`RabbitMQClusterTransport`) with an explicit queue mode and `DelayedDeliverySupport.Disabled` or `UnsafeEnabled`. [#795](https://github.com/Particular/NServiceBus.RabbitMQ/pull/795) introduced it with quorum queue support. It existed only in 7.0 prereleases, and #992 removed it. Once quorum queues could dead-letter at least once, users no longer needed to choose between no delays and unsafe delays.

## Open questions

- No public record shows whether the [`rabbitmq_delayed_message_exchange`](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange) plugin was evaluated, either in 2017 for the original design or in 2022 for v2. Ask the maintainers before proposing it.
- No public record explains why v1 messages are migrated by an operator command instead of automatically at endpoint startup.
