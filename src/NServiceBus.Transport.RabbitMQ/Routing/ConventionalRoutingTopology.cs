#nullable disable
namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;
    using NServiceBus.Logging;
    using Unicast.Messages;

    class ConventionalRoutingTopology : IRoutingTopology
    {
        public ConventionalRoutingTopology(bool durable, QueueType queueType)
        {
            this.durable = durable;
            this.queueType = queueType;
            exchangeNameConvention = type => type.Namespace + ":" + type.Name;
        }

        internal ConventionalRoutingTopology(bool durable, QueueType queueType, Func<Type, string> exchangeNameConvention)
        {
            this.queueType = queueType;
            this.durable = durable;
            this.exchangeNameConvention = exchangeNameConvention;
        }

        public async ValueTask SetupSubscription(IChannel channel, MessageMetadata type, string subscriberName, CancellationToken cancellationToken = default)
        {
            // Make handlers for IEvent handle all events whether they extend IEvent or not
            var typeToSubscribe = type.MessageType == typeof(IEvent) ? typeof(object) : type.MessageType;

            await SetupTypeSubscriptions(channel, typeToSubscribe, cancellationToken).ConfigureAwait(false);
            await channel.ExchangeBindAsync(subscriberName, exchangeNameConvention(typeToSubscribe), string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask TeardownSubscription(IChannel channel, MessageMetadata type, string subscriberName, CancellationToken cancellationToken = default)
        {
            try
            {
                await channel.ExchangeUnbindAsync(subscriberName, exchangeNameConvention(type.MessageType), string.Empty, null, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        public async ValueTask Publish(IChannel channel, Type type, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            await SetupTypeSubscriptions(channel, type, cancellationToken).ConfigureAwait(false);
            await channel.BasicPublishAsync(exchangeNameConvention(type), string.Empty, false, properties, message.Body, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask Send(IChannel channel, string address, OutgoingMessage message, BasicProperties properties, CancellationToken cancellationToken = default)
        {
            try
            {
                await channel
                    .BasicPublishAsync(address, string.Empty, true, properties, message.Body, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (PublishException e)
            {
                throw new Exception($"Message {message.MessageId} could not be routed to {address}", e);
            }
        }

        public ValueTask RawSendInCaseOfFailure(IChannel channel, string address, ReadOnlyMemory<byte> body, BasicProperties properties, CancellationToken cancellationToken = default)
            => channel.BasicPublishAsync(address, string.Empty, true, properties, body, cancellationToken);

        public async ValueTask Initialize(IChannel channel, IEnumerable<string> receivingAddresses, IEnumerable<string> sendingAddresses, CancellationToken cancellationToken = default)
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
                await CreateExchange(channel, address, cancellationToken).ConfigureAwait(false);
                await channel.QueueBindAsync(address, address, string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        public ValueTask BindToDelayInfrastructure(IChannel channel, string address, string deliveryExchange, string routingKey, CancellationToken cancellationToken = default)
            => new(channel.ExchangeBindAsync(address, deliveryExchange, routingKey, cancellationToken: cancellationToken));

        async ValueTask SetupTypeSubscriptions(IChannel channel, Type type, CancellationToken cancellationToken)
        {
            if (type == typeof(object) || IsTypeTopologyKnownConfigured(type))
            {
                return;
            }

            var typeToProcess = type;
            await CreateExchange(channel, exchangeNameConvention(typeToProcess), cancellationToken).ConfigureAwait(false);
            var baseType = typeToProcess.BaseType;

            while (baseType != null)
            {
                await CreateExchange(channel, exchangeNameConvention(baseType), cancellationToken).ConfigureAwait(false);
                await channel.ExchangeBindAsync(exchangeNameConvention(baseType), exchangeNameConvention(typeToProcess), string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
                typeToProcess = baseType;
                baseType = typeToProcess.BaseType;
            }

            foreach (var interfaceType in type.GetInterfaces())
            {
                var exchangeName = exchangeNameConvention(interfaceType);

                await CreateExchange(channel, exchangeName, cancellationToken).ConfigureAwait(false);
                await channel.ExchangeBindAsync(exchangeName, exchangeNameConvention(type), string.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            MarkTypeConfigured(type);
        }

        void MarkTypeConfigured(Type eventType) => typeTopologyConfiguredSet[eventType] = null;

        bool IsTypeTopologyKnownConfigured(Type eventType) => typeTopologyConfiguredSet.ContainsKey(eventType);

        async ValueTask CreateExchange(IChannel channel, string exchangeName, CancellationToken cancellationToken)
        {
            try
            {
                await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable, autoDelete: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // TODO: Any better way to make this idempotent?
            }
        }

        readonly bool durable;
        readonly QueueType queueType;
        readonly ConcurrentDictionary<Type, string> typeTopologyConfiguredSet = new ConcurrentDictionary<Type, string>();
        readonly Func<Type, string> exchangeNameConvention;

        static readonly ILog Logger = LogManager.GetLogger(typeof(ConventionalRoutingTopology));
    }
}