# Routing topology and queue declaration

How endpoints map sends, publishes, subscriptions, and queue declarations onto RabbitMQ exchanges and queues. For configuration, see the public [routing topology](https://docs.particular.net/transports/rabbitmq/routing-topology) documentation.

## The topology owns the broker layout

[`IRoutingTopology`](../src/NServiceBus.Transport.RabbitMQ/Routing/IRoutingTopology.cs) is the single place that knows the broker layout:

- sending and publishing, plus the raw send used to move poison messages;
- subscribing and unsubscribing, called by [`SubscriptionManager.cs`](../src/NServiceBus.Transport.RabbitMQ/Administration/SubscriptionManager.cs), which contains no layout knowledge itself;
- declaring queues, exchanges, and bindings (`Initialize`);
- binding an address to the delay infrastructure (`BindToDelayInfrastructure`).

The interface reached that shape in steps:

- Exchange creation moved into the topologies in 2013 ([17e35d9e](https://github.com/Particular/NServiceBus.RabbitMQ/commit/17e35d9ed6ccc8c7323d657b60f74301986f73b5)), after queue creation had failed to create the exchange senders expected ([336e3dd5](https://github.com/Particular/NServiceBus.RabbitMQ/commit/336e3dd54eb6eed897224d71936193b76328431f)).
- Queue declaration moved into the topology after a user needed to integrate the `rabbitmq-sharding` plugin, which must own the main queue ([#248](https://github.com/Particular/NServiceBus.RabbitMQ/issues/248), [#263](https://github.com/Particular/NServiceBus.RabbitMQ/pull/263)). Receiving and sending addresses deliberately stay separate parameters.
- Version 5 folded the separate `IDeclareQueues` and `ISupportDelayedDelivery` interfaces into `IRoutingTopology` ([#412](https://github.com/Particular/NServiceBus.RabbitMQ/pull/412), [#421](https://github.com/Particular/NServiceBus.RabbitMQ/pull/421)).

Custom topologies implement this public interface. Changing it is a breaking change for them. Version 10 changed it to RabbitMQ.Client 7's asynchronous `IChannel` API ([#1446](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1446)).

## The built-in topologies

Both topologies exist since the 1.x code base. All endpoints that communicate must use the same topology.

**Conventional** ([`ConventionalRoutingTopology.cs`](../src/NServiceBus.Transport.RabbitMQ/Routing/ConventionalRoutingTopology.cs)):

- Every endpoint queue has a fanout exchange of the same name, bound to it. Sends publish to that exchange.
- Every event type, base class, and implemented interface gets a fanout exchange. Exchanges are bound child to parent, so a subscriber to a base type or an interface receives derived events.
- Publishing creates the type hierarchy lazily, and caches which types are already configured.
- This is the topology that supports polymorphic events fully. The public documentation recommends it.

**Direct** ([`DirectRoutingTopology.cs`](../src/NServiceBus.Transport.RabbitMQ/Routing/DirectRoutingTopology.cs)):

- Sends go to the default exchange, with the queue name as routing key.
- Events are published to one topic exchange (default `amq.topic`). The routing key is built by [`DefaultRoutingKeyConvention.cs`](../src/NServiceBus.Transport.RabbitMQ/Routing/DefaultRoutingKeyConvention.cs) from the type's non-system base classes and its first non-system interface.
- Subscribers bind with `<key>.#`, or `#` for `IEvent` and `object`.
- A type with more than one relevant interface cannot be routed to all of its subscribers. The transport logs a warning ([7cdeecec](https://github.com/Particular/NServiceBus.RabbitMQ/commit/7cdeececd469d757a643b56fb8665df0c5abb696)).
- Routing keys are limited to 255 bytes by AMQP.

## Explicit choices

- **Topology.** Since version 5, endpoints must choose a topology explicitly. Previously the transport fell back to conventional ([#427](https://github.com/Particular/NServiceBus.RabbitMQ/issues/427), [#428](https://github.com/Particular/NServiceBus.RabbitMQ/pull/428)).
- **Queue type.** Since version 7, the queue type (`QueueType.Classic` or `QueueType.Quorum`) is a required part of the topology configuration ([`RoutingTopology.cs`](../src/NServiceBus.Transport.RabbitMQ/Configuration/RoutingTopology.cs)). The transport does not assume a type for an existing broker:
  - Quorum queues are declared with `x-queue-type=quorum` and are always durable. A non-durable setting is ignored with a warning.
  - RabbitMQ rejects a declaration whose queue type differs from an existing queue. Since version 7, installers surface that error instead of suppressing it ([6 to 7 upgrade guide](https://docs.particular.net/transports/upgrades/rabbitmq-6to7)).
  - Queue type cannot be changed in place. `rabbitmq-transport queue migrate-to-quorum` ([#1006](https://github.com/Particular/NServiceBus.RabbitMQ/pull/1006)) moves messages through a holding queue, recreates the queue, and can be rerun after a failure. It does not support the direct topology ([classic to quorum migration](https://docs.particular.net/transports/upgrades/rabbitmq-classic-to-quorum-migration)).
  - Stream queues are recognized in management API responses but are not a supported endpoint queue type.
- **Durability.** Entity durability (`useDurableEntities`) and message persistence are independent settings ([#443](https://github.com/Particular/NServiceBus.RabbitMQ/pull/443)).

## Addresses

`RabbitMQTransportInfrastructure.TranslateAddress` builds a queue name from the endpoint name, plus `-<discriminator>` and `.<qualifier>` when present. The queue name is also the conventional topology's exchange name and the direct topology's send routing key.

## Decisions

- Topology selection is required — [#428](https://github.com/Particular/NServiceBus.RabbitMQ/pull/428), motivated in [#427](https://github.com/Particular/NServiceBus.RabbitMQ/issues/427)
- Custom topologies receive the durability setting through a factory — [#250](https://github.com/Particular/NServiceBus.RabbitMQ/pull/250)
- Queue declaration belongs to the topology — [#248](https://github.com/Particular/NServiceBus.RabbitMQ/issues/248), [#263](https://github.com/Particular/NServiceBus.RabbitMQ/pull/263)
- Quorum queue support and the queue-type mismatch compatibility window in 6.1 — [#795](https://github.com/Particular/NServiceBus.RabbitMQ/pull/795), [#805](https://github.com/Particular/NServiceBus.RabbitMQ/pull/805)
