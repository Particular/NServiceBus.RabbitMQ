namespace NServiceBus.Transports.RabbitMQ
{
    using global::RabbitMQ.Client;

    /// <summary>
    /// Allows users to provide their own connection strategies.
    /// </summary>
    public interface IManageRabbitMqConnections
    {
        /// <summary>
        /// Gets a connection for outgoing operations.
        /// </summary>
        IConnection GetPublishConnection();

        /// <summary>
        /// Gets a connection for consuming messages from the broker.
        /// </summary>
        IConnection GetConsumeConnection();

        /// <summary>
        /// Gets an admin connection to create queues, exchanges etc.
        /// </summary>
        IConnection GetAdministrationConnection();
    }
}