namespace NServiceBus.Features
{
    using System;
    using System.Configuration;
    using CircuitBreakers;
    using Config;
    using Pipeline;
    using RabbitMQ.Client.Events;
    using Settings;
    using Support;
    using Transports;
    using Transports.RabbitMQ;
    using Transports.RabbitMQ.Config;
    using Transports.RabbitMQ.Connection;
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
            var errorQueue = context.Settings.GetConfigSection<MessageForwardingInCaseOfFaultConfig>()?.ErrorQueue;

            MessageConverter messageConverter;

            if (context.Settings.HasSetting(CustomMessageIdStrategy))
            {
                messageConverter = new MessageConverter(context.Settings.Get<Func<BasicDeliverEventArgs, string>>(CustomMessageIdStrategy));
            }
            else
            {
                messageConverter = new MessageConverter();
            }

            string hostDisplayName;
            if (!context.Settings.TryGet("NServiceBus.HostInformation.DisplayName", out hostDisplayName))//this was added in 5.1.2 of the core
            {
                hostDisplayName = RuntimeEnvironment.MachineName;
            }

            var consumerTag = string.Format("{0} - {1}", hostDisplayName, context.Settings.EndpointName());

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
            context.Settings.GetOrDefault<bool>("Transport.PurgeOnStartup"),
            consumerTag);



            context.Container.RegisterSingleton(connectionConfiguration);

            context.Container.ConfigureComponent(builder => new RabbitMqDequeueStrategy(
                builder.Build<IManageRabbitMqConnections>(),
                SetupCircuitBreaker(builder.Build<CriticalError>()),
                receiveOptions,
                errorQueue), DependencyLifecycle.InstancePerCall);


            context.Container.ConfigureComponent<OpenPublishChannelBehavior>(DependencyLifecycle.InstancePerCall);

            context.Pipeline.Register<OpenPublishChannelBehavior.Registration>();
            context.Pipeline.Register<ReadIncomingCallbackAddressBehavior.Registration>();

            context.Container.ConfigureComponent(b => new RabbitMqMessageSender(b.Build<IRoutingTopology>(), b.Build<IChannelProvider>(), b.Build<PipelineExecutor>().CurrentContext),  DependencyLifecycle.InstancePerCall);

            if (useCallbackReceiver)
            {
                context.Container.ConfigureComponent<CallbackQueueCreator>(DependencyLifecycle.InstancePerCall)
                    .ConfigureProperty(p => p.Enabled, true)
                    .ConfigureProperty(p => p.CallbackQueueAddress, Address.Parse(callbackQueue));

                //context.Pipeline.Register<ForwardCallbackQueueHeaderBehavior.Registration>();
                context.Pipeline.Register<SetOutgoingCallbackAddressBehavior.Registration>();
                context.Container.ConfigureComponent<SetOutgoingCallbackAddressBehavior>(DependencyLifecycle.SingleInstance)
                    .ConfigureProperty(p => p.CallbackQueue, callbackQueue);
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
                    topology = new DirectRoutingTopology(conventions, durable);
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

                context.Container.ConfigureComponent(builder => new RabbitMqConnectionFactory(builder.Build<ConnectionConfiguration>()), DependencyLifecycle.InstancePerCall);
            }
        }

        static RepeatedFailuresOverTimeCircuitBreaker SetupCircuitBreaker(CriticalError criticalError)
        {

            var timeToWaitBeforeTriggering = TimeSpan.FromMinutes(2);
            var timeToWaitBeforeTriggeringOverride = ConfigurationManager.AppSettings["NServiceBus/RabbitMqDequeueStrategy/TimeToWaitBeforeTriggering"];

            if (!string.IsNullOrEmpty(timeToWaitBeforeTriggeringOverride))
            {
                timeToWaitBeforeTriggering = TimeSpan.Parse(timeToWaitBeforeTriggeringOverride);
            }

            var delayAfterFailure = TimeSpan.FromSeconds(5);
            var delayAfterFailureOverride = ConfigurationManager.AppSettings["NServiceBus/RabbitMqDequeueStrategy/DelayAfterFailure"];

            if (!string.IsNullOrEmpty(delayAfterFailureOverride))
            {
                delayAfterFailure = TimeSpan.Parse(delayAfterFailureOverride);
            }

            return new RepeatedFailuresOverTimeCircuitBreaker("RabbitMqConnectivity",
                timeToWaitBeforeTriggering, 
                ex => criticalError.Raise("Repeated failures when communicating with the broker",
                    ex), delayAfterFailure);
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