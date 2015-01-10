namespace NServiceBus.Features
{
    using System;
    using EasyNetQ;
    using RabbitMQ.Client.Events;
    using Settings;
    using Support;
    using Transports;
    using Transports.RabbitMQ;
    using Transports.RabbitMQ.Config;
    using Transports.RabbitMQ.Routing;

    class RabbitMqTransportFeature : ConfigureTransport
    {
        public const string UseCallbackReceiverSettingKey = "RabbitMQ.UseCallbackReceiver";
        public const string MaxConcurrencyForCallbackReceiver = "RabbitMQ.MaxConcurrencyForCallbackReceiver";
        public const string CustomMessageIdStrategy = "RabbitMQ.CustomMessageIdStrategy";

        public RabbitMqTransportFeature()
        {
            Defaults(s =>
            {
                s.SetDefault(UseCallbackReceiverSettingKey, true);

                s.SetDefault(MaxConcurrencyForCallbackReceiver, 1);
            });
        }

        protected override string GetLocalAddress(ReadOnlySettings settings)
        {
            return Address.Parse(settings.Get<string>("NServiceBus.LocalAddress")).Queue;
        }

        protected override void Configure(FeatureConfigurationContext context, string connectionString)
        {
            var useCallbackReceiver = context.Settings.Get<bool>(UseCallbackReceiverSettingKey);
            var maxConcurrencyForCallbackReceiver = context.Settings.Get<int>(MaxConcurrencyForCallbackReceiver);
            var queueName = GetLocalAddress(context.Settings);
            var callbackQueue = string.Format("{0}.{1}", queueName, RuntimeEnvironment.MachineName);
            var connectionConfiguration = new ConnectionStringParser(context.Settings).Parse(connectionString);

            MessageConverter messageConverter;

            if (context.Settings.HasSetting(CustomMessageIdStrategy))
            {
                messageConverter = new MessageConverter(context.Settings.Get<Func<BasicDeliverEventArgs, string>>(CustomMessageIdStrategy));
            }
            else
            {
                messageConverter = new MessageConverter();
            }

            var receiveOptions = new ReceiveOptions(workQueue =>
            {
                //if this isn't the main queue we shouldn't use callback receiver
                if (!useCallbackReceiver || workQueue != queueName)
                {
                    return SecondaryReceiveSettings.Disabled();
                }
                return SecondaryReceiveSettings.Enabled(callbackQueue, maxConcurrencyForCallbackReceiver);
            },
            messageConverter,
            connectionConfiguration.PrefetchCount, 
            connectionConfiguration.DequeueTimeout * 1000,
            context.Settings.GetOrDefault<bool>("Transport.PurgeOnStartup"));

        
     
            context.Container.RegisterSingleton(connectionConfiguration);
            
            context.Container.ConfigureComponent(builder => new RabbitMqDequeueStrategy(
                builder.Build<IManageRabbitMqConnections>(),
                builder.Build<CriticalError>(),
                receiveOptions),DependencyLifecycle.InstancePerCall);


            context.Container.ConfigureComponent<OpenPublishChannelBehavior>(DependencyLifecycle.InstancePerCall);

            context.Pipeline.Register<OpenPublishChannelBehavior.Registration>();

            context.Container.ConfigureComponent<RabbitMqMessageSender>(DependencyLifecycle.InstancePerCall);
       
            if (useCallbackReceiver)
            {
                context.Container.ConfigureProperty<RabbitMqMessageSender>(p => p.CallbackQueue, callbackQueue);
                context.Container.ConfigureComponent<CallbackQueueCreator>(DependencyLifecycle.InstancePerCall)
                    .ConfigureProperty(p => p.Enabled, true)
                    .ConfigureProperty(p => p.CallbackQueueAddress, Address.Parse(callbackQueue));

                context.Pipeline.Register<ForwardCallbackQueueHeaderBehavior.Registration>();
            }

            context.Container.ConfigureComponent<ChannelProvider>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.UsePublisherConfirms, connectionConfiguration.UsePublisherConfirms)
                  .ConfigureProperty(p => p.MaxWaitTimeForConfirms, connectionConfiguration.MaxWaitTimeForConfirms);
            
            context.Container.ConfigureComponent<RabbitMqDequeueStrategy>(DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent<RabbitMqMessagePublisher>(DependencyLifecycle.InstancePerCall);

            context.Container.ConfigureComponent<RabbitMqSubscriptionManager>(DependencyLifecycle.SingleInstance)
             .ConfigureProperty(p => p.EndpointQueueName, queueName);

            context.Container.ConfigureComponent<RabbitMqQueueCreator>(DependencyLifecycle.InstancePerCall);

            if (context.Settings.HasSetting<IRoutingTopology>())
            {
                context.Container.RegisterSingleton(context.Settings.Get<IRoutingTopology>());
            }
            else
            {
                var durable = GetDurableMessagesEnabled(context.Settings);

                IRoutingTopology topology;

                DirectRoutingTopology.Conventions conventions;


                if (context.Settings.TryGet(out conventions))
                {
                    topology = new DirectRoutingTopology(conventions,durable);    
                }
                else
                {
                    topology = new ConventionalRoutingTopology(durable);    
                }
                

                context.Container.RegisterSingleton(topology);
            }

            if (context.Settings.HasSetting("IManageRabbitMqConnections"))
            {
                context.Container.ConfigureComponent(context.Settings.Get<Type>("IManageRabbitMqConnections"), DependencyLifecycle.SingleInstance);
            }
            else
            {
                context.Container.ConfigureComponent<RabbitMqConnectionManager>(DependencyLifecycle.SingleInstance);

                context.Container.ConfigureComponent<IConnectionFactory>(builder => new ConnectionFactoryWrapper(builder.Build<IConnectionConfiguration>(), new DefaultClusterHostSelectionStrategy<ConnectionFactoryInfo>()), DependencyLifecycle.InstancePerCall);
            }
        }

        static bool GetDurableMessagesEnabled(ReadOnlySettings settings)
        {
            bool durableMessagesEnabled;
            if (settings.TryGet("Endpoint.DurableMessages", out durableMessagesEnabled))
            {
                return durableMessagesEnabled;
            }
            return true;
        }


        protected override string ExampleConnectionStringForErrorMessage
        {
            get { return "host=localhost"; }
        }

    }
}