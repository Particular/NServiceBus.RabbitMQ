namespace NServiceBus.Transport.RabbitMQ
{
    using global::RabbitMQ.Client;

    /// <summary>
    /// An extension for <see cref="IRoutingTopology"/> implementations which allows
    /// for receiving messages from the delay infrastructure.
    /// </summary>
    public interface ISupportDelayedDelivery
    {
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
