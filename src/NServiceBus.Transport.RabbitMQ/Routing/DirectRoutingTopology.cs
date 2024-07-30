#nullable disable
namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::RabbitMQ.Client;
    using NServiceBus.Logging;
    using Unicast.Messages;

    class DirectRoutingTopology : IRoutingTopology
    {
        public DirectRoutingTopology(bool durable, QueueType queueType, Func<Type, string> routingKeyConvention = null, Func<string> exchangeNameConvention = null)
        {
            this.durable = durable;
            this.queueType = queueType;
            this.routingKeyConvention = routingKeyConvention ?? DefaultRoutingKeyConvention.GenerateRoutingKey;
            this.exchangeNameConvention = exchangeNameConvention ?? (() => amqpTopicExchange);
        }

        public async Task SetupSubscription(IChannel channel, MessageMetadata type, string subscriberName, CancellationToken cancellationToken = default)
        {
            await CreateExchange(channel, exchangeNameConvention(), cancellationToken).ConfigureAwait(false);
            await channel.QueueBindAsync(subscriberName, exchangeNameConvention(), GetRoutingKeyForBinding(type.MessageType), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public Task TeardownSubscription(IChannel channel, MessageMetadata type, string subscriberName, CancellationToken cancellationToken = default)
        {
            return channel.QueueUnbindAsync(subscriberName, exchangeNameConvention(), GetRoutingKeyForBinding(type.MessageType), null, cancellationToken);
        }

        public async Task Publish(IChannel channel, Type type, OutgoingMessage message, IBasicProperties properties, CancellationToken cancellationToken = default)
        {
            await channel.BasicPublishAsync(exchangeNameConvention(), GetRoutingKeyForPublish(type), false, properties, message.Body).ConfigureAwait(false);
        }

        public Task Send(IChannel channel, string address, OutgoingMessage message, IBasicProperties properties, CancellationToken cancellationToken = default)
        {
            channel.BasicPublishAsync(string.Empty, address, true, properties, message.Body);
        }

        public Task RawSendInCaseOfFailure(IChannel channel, string address, ReadOnlyMemory<byte> body, IBasicProperties properties, CancellationToken cancellationToken = default)
        {
            channel.BasicPublishAsync(string.Empty, address, true, properties, body);
        }

        public async Task Initialize(IChannel channel, IEnumerable<string> receivingAddresses, IEnumerable<string> sendingAddresses, CancellationToken cancellationToken = default)
        {
            Dictionary<string, object> arguments = null;
            var createDurableQueue = durable;

            if (queueType == QueueType.Quorum)
            {
                arguments = new Dictionary<string, object> { { "x-queue-type", "quorum" } };

                if (createDurableQueue == false)
                {
                    createDurableQueue = true;
                    Logger.Warn("Quorum queues are always durable, so the non-durable setting is being ignored for queue declaration.");
                }
            }

            foreach (var address in receivingAddresses.Concat(sendingAddresses))
            {
                await channel.QueueDeclareAsync(address, createDurableQueue, false, false, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        public Task BindToDelayInfrastructure(IChannel channel, string address, string deliveryExchange, string routingKey, CancellationToken cancellationToken = default)
            => channel.QueueBindAsync(address, deliveryExchange, routingKey, cancellationToken: cancellationToken);

        async Task CreateExchange(IChannel channel, string exchangeName, CancellationToken cancellationToken)
        {
            if (exchangeName == amqpTopicExchange)
            {
                return;
            }

            try
            {
                // TODO cancellation token
                await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        string GetRoutingKeyForPublish(Type eventType) => routingKeyConvention(eventType);

        string GetRoutingKeyForBinding(Type eventType)
        {
            if (eventType == typeof(IEvent) || eventType == typeof(object))
            {
                return "#";
            }

            return routingKeyConvention(eventType) + ".#";
        }

        const string amqpTopicExchange = "amq.topic";

        readonly bool durable;
        readonly QueueType queueType;
        readonly Func<Type, string> routingKeyConvention;
        readonly Func<string> exchangeNameConvention;

        static readonly ILog Logger = LogManager.GetLogger(typeof(DirectRoutingTopology));
    }
}