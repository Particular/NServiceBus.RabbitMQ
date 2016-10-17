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
            //Make sure autosubscribe is disabled
            if (context.Settings.IsFeatureEnabled(typeof(AutoSubscribe)))
            {
                throw new Exception("Automatic routing topology cannot be used when auto subscribe is enabled. Please disable the auto subscribe feature.");
            }

            var conventions = context.Settings.Get<Conventions>();
            var topology = (AutomaticRoutingTopology) context.Settings.Get<IRoutingTopology>();
            var transportInfrastructure = (RabbitMQTransportInfrastructure)context.Settings.Get<TransportInfrastructure>();

            context.Pipeline.Replace("UnicastSendRouterConnector", new MulticastSendRouterConnector());
            if (!context.Settings.GetOrDefault<bool>("Endpoint.SendOnly"))
            {
                context.RegisterStartupTask(b =>
                {
                    var handlerRegistry = b.Build<MessageHandlerRegistry>();
                    var handledMessageTypes = GetMessageTypesHandledByThisEndpoint(handlerRegistry, conventions);
                    return new ApplyBindings(transportInfrastructure.ConnectionFactory, context.Settings.LocalAddress(), handledMessageTypes, topology);
                });
            }
        }

        static List<Type> GetMessageTypesHandledByThisEndpoint(MessageHandlerRegistry handlerRegistry, Conventions conventions)
        {
            var messageTypesHandled = handlerRegistry.GetMessageTypes() //get all potential messages
                .Where(t => !conventions.IsInSystemConventionList(t)) //never auto-wire system messages
                .ToList();

            return messageTypesHandled;
        }

        class ApplyBindings : FeatureStartupTask
        {
            public ApplyBindings(ConnectionFactory connectionFactory, string inputQueue, IEnumerable<Type> messagesHandledByThisEndpoint, AutomaticRoutingTopology topology)
            {
                this.connectionFactory = connectionFactory;
                this.inputQueue = inputQueue;
                this.messagesHandledByThisEndpoint = messagesHandledByThisEndpoint;
                this.topology = topology;
            }

            protected override Task OnStart(IMessageSession session)
            {
                var detourExchangeName = $"Commands-{inputQueue}-{Guid.NewGuid()}";
                var localFanoutExchange = $"Commands-{inputQueue}";

                using (var connection = connectionFactory.CreateAdministrationConnection())
                using (var channel = connection.CreateModel())
                {
                    channel.ExchangeDeclare(detourExchangeName, "fanout", true, true, null);
                    channel.QueueBind(inputQueue, detourExchangeName, string.Empty);

                    topology.BindMessageTypes(channel, detourExchangeName, messagesHandledByThisEndpoint);

                    channel.ExchangeDelete(localFanoutExchange);
                    channel.ExchangeDeclare(localFanoutExchange, "fanout", true, true, null);
                    channel.QueueBind(inputQueue, localFanoutExchange, string.Empty);

                    topology.BindMessageTypes(channel, localFanoutExchange, messagesHandledByThisEndpoint);

                    channel.ExchangeDelete(detourExchangeName);
                }
                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return TaskEx.CompletedTask;
            }

            ConnectionFactory connectionFactory;
            string inputQueue;
            IEnumerable<Type> messagesHandledByThisEndpoint;
            readonly AutomaticRoutingTopology topology;
        }
    }
}