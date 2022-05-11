
namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using global::RabbitMQ.Client;
    using Unicast.Messages;


    /// <summary>
    /// Topology for routing messages on the transport.
    /// </summary>
    public interface IRoutingTopology
    {
        /// <summary>
        /// Sets up a subscription for the subscriber to the specified type.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="type">The type to subscribe to.</param>
        /// <param name="subscriberName">The name of the subscriber.</param>
        void SetupSubscription(IModel channel, MessageMetadata type, string subscriberName);

        /// <summary>
        /// Removes a subscription for the subscriber to the specified type.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="type">The type to unsubscribe from.</param>
        /// <param name="subscriberName">The name of the subscriber.</param>
        void TeardownSubscription(IModel channel, MessageMetadata type, string subscriberName);

        /// <summary>
        /// Publishes a message of the specified type.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="type">The type of the message to be published.</param>
        /// <param name="message">The message to publish.</param>
        /// <param name="properties">The RabbitMQ properties of the message to publish.</param>
        void Publish(IModel channel, Type type, OutgoingMessage message, IBasicProperties properties);

        /// <summary>
        /// Sends a message to the specified endpoint.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="address">The address of the destination endpoint.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="properties">The RabbitMQ properties of the message to send.</param>
        void Send(IModel channel, string address, OutgoingMessage message, IBasicProperties properties);

        /// <summary>
        /// Sends a raw message body to the specified endpoint.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="address">The address of the destination endpoint.</param>
        /// <param name="body">The raw message body to send.</param>
        /// <param name="properties">The RabbitMQ properties of the message to send.</param>
        void RawSendInCaseOfFailure(IModel channel, string address, ReadOnlyMemory<byte> body, IBasicProperties properties);

        /// <summary>
        /// Declares queues and performs any other initialization logic needed (e.g. creating exchanges and bindings).
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="receivingAddresses">
        /// The addresses of the queues to declare and perform initialization for, that this endpoint is receiving from.
        /// </param>
        /// <param name="sendingAddresses">
        /// The addresses of the queues to declare and perform initialization for, that this endpoint is sending to.
        /// </param>
        /// <param name="useQuorumQueues">
        /// Should the queues that this endpoint receieves from be created as quorum queues.
        /// </param>
        void Initialize(IModel channel, IEnumerable<string> receivingAddresses, IEnumerable<string> sendingAddresses, bool useQuorumQueues);

        /// <summary>
        /// Binds an address to the delay infrastructure's delivery exchange.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="address">The address that needs to be bound to the delivery exchange.</param>
        /// <param name="deliveryExchange">The name of the delivery exchange.</param>
        /// <param name="routingKey">The routing key required for the binding.</param>
        void BindToDelayInfrastructure(IModel channel, string address, string deliveryExchange, string routingKey);
    }
}