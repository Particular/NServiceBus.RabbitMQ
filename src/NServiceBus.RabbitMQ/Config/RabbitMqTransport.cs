﻿namespace NServiceBus.Features
{
    using System;
    using EasyNetQ;
    using Settings;
    using Support;
    using Transports;
    using Transports.RabbitMQ;
    using Transports.RabbitMQ.Config;
    using Transports.RabbitMQ.Routing;

    class RabbitMqTransport : ConfigureTransport
    {
        public RabbitMqTransport()
        {
            Defaults(s =>
            {
                s.SetDefault("RabbitMQ.UseCallbackReceiver", true);

                s.SetDefault("RabbitMQ.MaxConcurrencyForCallbackReceiver", 1);
            });
        }

        protected override string GetLocalAddress(ReadOnlySettings settings)
        {
            return settings.EndpointName();
        }

        protected override void Configure(FeatureConfigurationContext context, string connectionString)
        {
            var useCallbackReceiver = context.Settings.Get<bool>("RabbitMQ.UseCallbackReceiver");
            var maxConcurrencyForCallbackReceiver = context.Settings.Get<int>("RabbitMQ.MaxConcurrencyForCallbackReceiver");

            var queueName = GetLocalAddress(context.Settings);
            var callbackQueue = string.Format("{0}.{1}", queueName, RuntimeEnvironment.MachineName);

            var connectionConfiguration = new ConnectionStringParser(context.Settings).Parse(connectionString);

            context.Container.RegisterSingleton(connectionConfiguration);

            context.Container.ConfigureComponent<RabbitMqDequeueStrategy>(DependencyLifecycle.InstancePerCall)
                 .ConfigureProperty(p => p.PrefetchCount, connectionConfiguration.PrefetchCount);

            context.Container.ConfigureComponent<OpenPublishChannelBehavior>(DependencyLifecycle.InstancePerCall);

            context.Pipeline.Register<OpenPublishChannelBehavior.Registration>();

            if (useCallbackReceiver)
            {
                context.Container.ConfigureComponent<CallbackQueueCreator>(DependencyLifecycle.InstancePerCall)
                    .ConfigureProperty(p => p.Enabled, true)
                    .ConfigureProperty(p => p.CallbackQueueAddress, Address.Parse(callbackQueue));

                context.Container.ConfigureComponent<PromoteCallbackQueueBehavior>(DependencyLifecycle.InstancePerCall);
                context.Pipeline.Register<PromoteCallbackQueueBehavior.Registration>();
            }

            context.Container.ConfigureComponent<ChannelProvider>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.UsePublisherConfirms, connectionConfiguration.UsePublisherConfirms)
                  .ConfigureProperty(p => p.MaxWaitTimeForConfirms, connectionConfiguration.MaxWaitTimeForConfirms);
            context.Container.RegisterSingleton(new SecondaryReceiveConfiguration(workQueue =>
            {
                //if this isn't the main queue we shouldn't use callback receiver
                if (!useCallbackReceiver || workQueue != queueName)
                {
                    return new SecondaryReceiveSettings();
                }

                var settings = new SecondaryReceiveSettings
                {
                    MaximumConcurrencyLevel = maxConcurrencyForCallbackReceiver
                };


                settings.SecondaryQueues.Add(callbackQueue);

                return settings;
            }));

            context.Container.ConfigureComponent<RabbitMqDequeueStrategy>(DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent<RabbitMqMessageSender>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(p => p.CallbackQueue, callbackQueue);
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
                context.Container.ConfigureComponent<ConventionalRoutingTopology>(DependencyLifecycle.SingleInstance);
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


        protected override string ExampleConnectionStringForErrorMessage
        {
            get { return "host=localhost"; }
        }
    }
}