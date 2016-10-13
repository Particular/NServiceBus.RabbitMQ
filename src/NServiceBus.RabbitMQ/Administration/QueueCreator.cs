namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using global::RabbitMQ.Client;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class QueueCreator : ICreateQueues
    {
        readonly ConnectionFactory connectionFactory;
        readonly IRoutingTopology routingTopology;
        readonly bool durableMessagesEnabled;

        public QueueCreator(ConnectionFactory connectionFactory, IRoutingTopology routingTopology, bool durableMessagesEnabled)
        {
            this.connectionFactory = connectionFactory;
            this.routingTopology = routingTopology;
            this.durableMessagesEnabled = durableMessagesEnabled;
        }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            foreach (var receivingAddress in queueBindings.ReceivingAddresses)
            {
                CreateQueueIfNecessary(receivingAddress);
            }

            foreach (var sendingAddress in queueBindings.SendingAddresses)
            {
                CreateQueueIfNecessary(sendingAddress);
            }

            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                for (var i = 9; i >= 0; i--)
                {
                    var nextLevelExchangeName = i == 0 ? "delay-triggered" : $"delay-exchange-level-{i - 1}";

                    channel.ExchangeDeclare($"delay-exchange-level-{i}", "headers", true);

                    var argumentsWait = new Dictionary<string, object>();
                    argumentsWait.Add("x-message-ttl", 1000*(int)Math.Pow(2, i));
                    argumentsWait.Add("x-dead-letter-exchange", nextLevelExchangeName);
                    channel.QueueDeclare($"delay-exchange-level-{i}-wait", true, false, false, argumentsWait);

                    var bindArgumentsWait = new Dictionary<string, object>();
                    bindArgumentsWait.Add("x-match", "all");
                    bindArgumentsWait.Add($"delay-level-{i}", "1");
                    channel.QueueBind($"delay-exchange-level-{i}-wait", $"delay-exchange-level-{i}", "", bindArgumentsWait);
                }

                for (var i = 0; i <= 9; i++)
                {
                    var baseEchangeName = i == 0 ? "delay-triggered" : $"delay-exchange-level-{i-1}";

                    var bindArguments = new Dictionary<string, object>();
                    bindArguments.Add("x-match", "all");
                    bindArguments.Add($"delay-level-{i}", "0");

                    channel.ExchangeBind(baseEchangeName, $"delay-exchange-level-{i}", "", bindArguments);
                }
            }

            return TaskEx.CompletedTask;
        }

        void CreateQueueIfNecessary(string receivingAddress)
        {
            using (var connection = connectionFactory.CreateAdministrationConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(receivingAddress, durableMessagesEnabled, false, false, null);

                routingTopology.Initialize(channel, receivingAddress);
            }
        }
    }
}