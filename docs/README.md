# Engineering context

## About this repository

NServiceBus.RabbitMQ is the RabbitMQ transport for NServiceBus, producing the `NServiceBus.RabbitMQ` NuGet package and the `rabbitmq-transport` command line tool (`NServiceBus.Transport.RabbitMQ.CommandLine`). For local build and test steps, including running a broker in Docker, see the `README.md`.

- `src/NServiceBus.Transport.RabbitMQ/` — the transport: `Configuration/`, `Connection/` (connections, channels, publisher confirms), `Receiving/` (message pump, message conversion), `Sending/`, `Routing/` (routing topologies), `DelayedDelivery/` (delay infrastructure), and `Administration/` (broker verification, management API client, subscriptions, queue purging)
- `src/NServiceBus.Transport.RabbitMQ.CommandLine/` — the `rabbitmq-transport` tool: create endpoints and the delay infrastructure, migrate queues and delays, validate delivery limits
- `src/*Tests/` — unit tests (including public API approvals), the shared NServiceBus transport tests, and acceptance tests; all except unit tests need a running broker
- `src/targets/` — Bullseye targets that reset the test virtual host on a broker
- `src/msbuild/` — build-time generation of dependency version ranges
- `scripts/` — broker reset script for local testing
- `.github/workflows/` — CI pipelines (build and test against brokers, code analysis, release, dependency updates)

## Start here

- [RabbitMQ transport documentation](https://docs.particular.net/transports/rabbitmq/) — public documentation entry point
- [RabbitMQ transport upgrade guides](https://docs.particular.net/transports/upgrades/rabbitmq-10to11) — what changed between major versions and what users must do; one guide per major version, `rabbitmq-<from>to<to>`, plus the [classic to quorum queue migration](https://docs.particular.net/transports/upgrades/rabbitmq-classic-to-quorum-migration)
- [README.md](../README.md) — how to build, run, and test locally
- [Contributing](https://docs.particular.net/platform/contributing) — contribution process

## Architecture and design

This section points to sources that explain why the repository is designed the way it is. Each entry names the question its source answers. How-to material is in the pages linked under Start here.

- [Delayed delivery](delayed-delivery.md) — how the broker-side delay chain works, and which invariants (fixed-width routing key, sender-side binding, versioned entity names) changes must keep
- [Routing topology and queue declaration](routing-topology.md) — why `IRoutingTopology` owns the broker layout, how the conventional and direct topologies differ, and why topology and queue type are explicit choices
- [Broker verification and the management API](broker-verification.md) — what the transport checks before it starts, why it needs the management API, and which other repository depends on the management client
- [Receiving, retrying, and dispatching messages](message-processing.md) — the at-least-once contract: acknowledgements, attempt counting, poison messages, publisher confirms, the `mandatory` flag, and persistence
- [Connections, channels, and concurrency](connections-and-channels.md) — connection roles, recovery and the circuit breaker, prefetch, the shared publish channel, clusters, and TLS
- [Message conversion and native integration](message-conversion.md) — how AMQP properties and headers map to NServiceBus headers, and the rules for messages from non-NServiceBus senders

## Decisions and rationale

- [Architecture and design decisions](decisions/)
  - [Consume with manual acknowledgements only](decisions/2016-02-29-manual-acknowledgement-only.md) — why there is no auto-ack mode, and why only `ReceiveOnly` is supported
  - [Always use publisher confirms](decisions/2020-07-08-always-use-publisher-confirms.md) — why confirms cannot be turned off
  - [Build the delay infrastructure from quorum queues with at-least-once dead lettering](decisions/2022-05-17-quorum-queues-for-delay-infrastructure.md) — why v2 delays exist, and why the broker must be 3.10 or later with `stream_queue` enabled
  - [Perform immediate retries through broker redelivery](decisions/2022-09-13-immediate-retries-through-broker-redelivery.md) — why there is no in-process retry loop, and how attempts are counted
  - [Enforce an effectively unlimited delivery limit on quorum queues](decisions/2025-02-25-effectively-unlimited-quorum-delivery-limit.md) — why startup validates delivery limits, why the management API is required, and why the value is `100000`

### Decisions recorded in pull requests

A pull request is listed here only when it is the canonical record for a decision area: it establishes a durable constraint or convention, or rejects an alternative likely to return, and no `docs/` file or ADR covers it. Bug fixes and routine changes are not listed. Find them with `git log` and `gh pr view`.

- A major RabbitMQ.Client upgrade is a transport major, because the public `IRoutingTopology` exposes client types — [#1446](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1446)
- Endpoints must choose a routing topology explicitly instead of defaulting to conventional — [#428](https://github.com/Particular/NServiceBus.RabbitMQ/pull/428), motivated in [#427](https://github.com/Particular/NServiceBus.RabbitMQ/issues/427)

Keep this index current when a canonical source is added, replaced, or retired; link, do not copy.
