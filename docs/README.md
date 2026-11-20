# Engineering context

## About this repository

This repository implements the NServiceBus transport for RabbitMQ.

- `src/` — transport, migration tooling, and tests
- `.github/workflows/` — CI pipelines

## Start here

- [RabbitMQ transport documentation](https://docs.particular.net/transports/rabbitmq/) — supported broker versions, configuration, and usage
- [README.md](../README.md) — how to run the transport tests locally
- [Contributing](https://docs.particular.net/platform/contributing) — contribution process

## Architecture and design

- [Routing topology](https://docs.particular.net/transports/rabbitmq/routing-topology) — how the built-in topologies use exchanges and bindings to route messages
- [Delayed delivery](https://docs.particular.net/transports/rabbitmq/delayed-delivery) — how the transport uses queue TTL and dead-letter exchanges to schedule messages

## Upstream contracts

- [NServiceBus: message dispatchers](https://github.com/Particular/NServiceBus/blob/master/docs/message-dispatchers.md) — what the transport must honor when dispatching, including delivery properties and dispatch consistency
- [NServiceBus: message receivers](https://github.com/Particular/NServiceBus/blob/master/docs/message-receivers.md) — what the transport must honor when receiving, including concurrency and shutdown
- [NServiceBus: for downstream components](https://github.com/Particular/NServiceBus/blob/master/docs/README.md#for-downstream-components) — the index of Core contracts for downstream components

## Decisions and rationale

- [Architecture and design decisions](decisions/)

Keep this index current when a canonical source is added, replaced, or retired; link, do not copy.
