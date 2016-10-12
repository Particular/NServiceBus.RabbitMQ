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

                    var argumentsNoWait = new Dictionary<string, object>();
                    argumentsNoWait.Add("x-message-ttl", 0);
                    argumentsNoWait.Add("x-dead-letter-exchange", nextLevelExchangeName);
                    channel.QueueDeclare($"delay-exchange-level-{i}-no-wait", true, false, false, argumentsNoWait);

                    var bindArgumentsWait = new Dictionary<string, object>();
                    bindArgumentsWait.Add("x-match", "all");
                    bindArgumentsWait.Add($"delay-level-{i}", "1");
                    channel.QueueBind($"delay-exchange-level-{i}-wait", $"delay-exchange-level-{i}", "", bindArgumentsWait);

                    var bindArgumentsNoWait = new Dictionary<string, object>();
                    bindArgumentsNoWait.Add("x-match", "all");
                    bindArgumentsNoWait.Add($"delay-level-{i}", "0");
                    channel.QueueBind($"delay-exchange-level-{i}-no-wait", $"delay-exchange-level-{i}", "", bindArgumentsNoWait);
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