namespace NServiceBus
{
    /// <summary>
    /// Transport definition for RabbitMQ in cluster configuration.
    /// </summary>
    public class RabbitMQClusterTransport : RabbitMQTransport
    {
        /// <summary>
        /// Creates new instance of the RabbitMQ transport.
        /// </summary>
        /// <param name="topology">The custom topology to use.</param>
        /// <param name="connectionString">Connection string.</param>
        /// <param name="queueMode">The queue mode for receiving queues.</param>
        public RabbitMQClusterTransport(Topology topology, string connectionString, QueueMode queueMode)
            : base(GetBuiltInTopology(topology), connectionString, queueMode)
        {
        }
    }
}