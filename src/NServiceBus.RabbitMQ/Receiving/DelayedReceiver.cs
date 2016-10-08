namespace NServiceBus.Transport.RabbitMQ
{
    using System.Text;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;

    class DelayedReceiver
    {
        const string delayedDestinationHeader = "NServiceBus.Transport.RabbitMQ.DelayedDestination"; //need to put this somewhere shared

        readonly ConnectionFactory connectionFactory;
        readonly IChannelProvider channelProvider;

        TaskScheduler exclusiveScheduler;
        IConnection connection;
        EventingBasicConsumer consumer;

        public DelayedReceiver(ConnectionFactory connectionFactory, IChannelProvider channelProvider)
        {
            this.connectionFactory = connectionFactory;
            this.channelProvider = channelProvider;
        }

        public void Start()
        {
            exclusiveScheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;

            connection = connectionFactory.CreateConnection("DelayedReceiver");

            var channel = connection.CreateModel();

            //copying from ConventionalRoutingTopology for now
            channel.ExchangeDeclare("delay-triggered", "fanout", true);
            channel.QueueDeclare("delay-triggered", true, false, false, null);
            channel.QueueBind("delay-triggered", "delay-triggered", "");
            //will need some other way to do this through the topology

            channel.BasicQos(0, 1, false);

            consumer = new EventingBasicConsumer(channel);

            consumer.Received += Consumer_Received;

            channel.BasicConsume("delay-triggered", false, "consumerTag", consumer); //should get the queue name from the routing topology
        }

        public void Stop()
        {
            consumer.Received -= Consumer_Received;

            if (connection.IsOpen)
            {
                connection.Close();
            }
        }

        async void Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
        {
            eventArgs.BasicProperties.Headers.Remove("x-death");

            var address = Encoding.UTF8.GetString((byte[])eventArgs.BasicProperties.Headers[delayedDestinationHeader]);

            var channel = channelProvider.GetPublishChannel();

            try
            {
                await channel.RawSendInCaseOfFailure(address, eventArgs.Body, eventArgs.BasicProperties).ConfigureAwait(false);
            }
            finally
            {
                channelProvider.ReturnPublishChannel(channel);
            }

            await consumer.Model.BasicAckSingle(eventArgs.DeliveryTag, exclusiveScheduler).ConfigureAwait(false);
        }
    }
}
