namespace NServiceBus.Transports.RabbitMQ
{
    using global::RabbitMQ.Client;

    /// <summary>
    /// Allows users to provide their own connection strategies
    /// </summary>
    public interface IManageRabbitMqConnections
    {
        /// <summary>
        /// Gets a connection for outgoing operations
        /// </summary>
        /// <returns></returns>
        IConnection GetPublishConnection();
        /// <summary>
        /// Gets a connection for consuming messages from the broker
        /// </summary>
        /// <returns></returns>
        IConnection GetConsumeConnection();
        /// <summary>
        /// Get a admin connection to create queues, exchanges etc
        /// </summary>
        /// <returns></returns>
        IConnection GetAdministrationConnection();
    }
}