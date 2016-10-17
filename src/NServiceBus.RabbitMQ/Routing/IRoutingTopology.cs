namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using global::RabbitMQ.Client;

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
        void SetupSubscription(IModel channel, Type type, string subscriberName);

        /// <summary>
        /// Removes a subscription for the subscriber to the specified type.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="type">The type to unsubscribe from.</param>
        /// <param name="subscriberName">The name of the subscriber.</param>
        void TeardownSubscription(IModel channel, Type type, string subscriberName);

        /// <summary>
        /// Publishes a message of the specified type.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="operation">Transport operation.</param>
        /// <param name="properties">The RabbitMQ properties of the message to publish.</param>
        void Publish(IModel channel, IOutgoingTransportOperation operation, IBasicProperties properties);

        /// <summary>
        /// Sends a message to the specified endpoint.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="operation">Transport operation.</param>
        /// <param name="properties">The RabbitMQ properties of the message to send.</param>
        void Send(IModel channel, IOutgoingTransportOperation operation, IBasicProperties properties);
        
        /// <summary>
        /// Sends a raw message body to the specified endpoint.
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="address">The address of the destination endpoint.</param>
        /// <param name="body">The raw message body to send.</param>
        /// <param name="properties">The RabbitMQ properties of the message to send.</param>
        void RawSendInCaseOfFailure(IModel channel, string address, byte[] body, IBasicProperties properties);

        /// <summary>
        /// Performs any initialization logic needed (e.g., creating exchanges and bindings).
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="main">The name of the queue to perform initialization on.</param>
        void Initialize(IModel channel, string main);

        /// <summary>
        /// Gets the outbound routing policy for this topology.
        /// </summary>
        OutboundRoutingPolicy OutboundRoutingPolicy { get; }
    }
}