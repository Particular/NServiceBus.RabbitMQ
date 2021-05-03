namespace NServiceBus
{
    /// <summary>
    /// Transport definition for RabbitMQ in cluster configuration.
    /// </summary>
    public class RabbitMQClusterTransport : RabbitMQTransport
    {
        /// <summary>
        /// Creates new instance of the RabbitMQ transport to connect to a RabbitMQ cluster.
        /// </summary>
        /// <param name="topology">The custom topology to use.</param>
        /// <param name="connectionString">Connection string of the nodes.</param>
        /// <param name="queueMode">The queue mode for receiving queues.</param>
        /// <param name="delayedDeliverySupportConfiguration">The timeouts configuration.</param>
        public RabbitMQClusterTransport(Topology topology, string connectionString, QueueMode queueMode, DelayedDeliverySupport delayedDeliverySupportConfiguration)
            : base(GetBuiltInTopology(topology), connectionString, queueMode, delayedDeliverySupportConfiguration == DelayedDeliverySupport.UnsafeEnabled)
        {
        }

        /// <summary>
        /// Adds a new node for use within a cluster.
        /// </summary>
        /// <param name="host">The hostname of the node.</param>
        /// <param name="port">The port of the node.</param>
        public void AddClusterNode(string host, int port = 5672)
        {
            additionalHostnames.Add($"{host}:{port}");
        }
    }
}