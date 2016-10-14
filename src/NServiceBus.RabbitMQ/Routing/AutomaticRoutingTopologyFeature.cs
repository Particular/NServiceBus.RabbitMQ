namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Features;
    using RabbitMQ.Client;
    using Transport;
    using Transport.RabbitMQ;
    using Unicast;
    using ConnectionFactory = Transport.RabbitMQ.ConnectionFactory;

    class AutomaticRoutingTopologyFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var conventions = context.Settings.Get<Conventions>();
            var transportInfrastructure = (RabbitMQTransportInfrastructure)context.Settings.Get<TransportInfrastructure>();

            context.Pipeline.Replace("UnicastSendRouterConnector", new MulticastSendRouterConnector());

            context.RegisterStartupTask(b =>
            {
                var handlerRegistry = b.Build<MessageHandlerRegistry>();
                var messageTypesHandled = GetMessageTypesHandledByThisEndpoint(handlerRegistry, conventions);
                return new ApplyBindings(transportInfrastructure.ConnectionFactory, context.Settings.LocalAddress(), messageTypesHandled);
            });
        }

        static List<Type> GetMessageTypesHandledByThisEndpoint(MessageHandlerRegistry handlerRegistry, Conventions conventions)
        {
            var messageTypesHandled = handlerRegistry.GetMessageTypes() //get all potential messages
                .Where(t => !conventions.IsInSystemConventionList(t)) //never auto-wire system messages
                .Where(t => !conventions.IsEventType(t)) //events should not be wired this way
                .ToList();

            return messageTypesHandled;
        }

        class ApplyBindings : FeatureStartupTask
        {
            public ApplyBindings(ConnectionFactory connectionFactory, string inputQueue, IEnumerable<Type> messagesHandledByThisEndpoint)
            {
                this.connectionFactory = connectionFactory;
                this.inputQueue = inputQueue;
                this.messagesHandledByThisEndpoint = messagesHandledByThisEndpoint;
            }

            protected override Task OnStart(IMessageSession session)
            {
                var detourExchangeName = $"Commands-{inputQueue}-{Guid.NewGuid()}";
                var localFanoutExchange = $"Commands-{inputQueue}";

                using (var connection = connectionFactory.CreateAdministrationConnection())
                using (var channel = connection.CreateModel())
                {
                    channel.ExchangeDeclare(detourExchangeName, "fanout", true);
                    channel.QueueBind(inputQueue, detourExchangeName, string.Empty);
                    BindMessageTypes(channel, detourExchangeName);

                    channel.ExchangeDelete(localFanoutExchange);
                    channel.ExchangeDeclare(localFanoutExchange, "fanout", true);
                    channel.QueueBind(inputQueue, localFanoutExchange, string.Empty);
                    BindMessageTypes(channel, localFanoutExchange);

                    channel.ExchangeDelete(detourExchangeName);
                }
                return Task.FromResult(0);
            }

            void BindMessageTypes(IModel channel, string localFanoutExchange)
            {
                foreach (var type in messagesHandledByThisEndpoint)
                {
                    var arguments = new Dictionary<string, object>
                    {
                        ["Type"] = type.AssemblyQualifiedName
                    };
                    channel.ExchangeBind(localFanoutExchange, "Commands", string.Empty, arguments);
                }
            }

            protected override Task OnStop(IMessageSession session)
            {
                return TaskEx.CompletedTask;
            }

            ConnectionFactory connectionFactory;
            string inputQueue;
            IEnumerable<Type> messagesHandledByThisEndpoint;
        }
    }
}