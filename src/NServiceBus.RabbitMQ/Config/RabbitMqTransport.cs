namespace NServiceBus.Features
{
    using System;
    using EasyNetQ;
    using Transports;
    using Transports.RabbitMQ;
    using Transports.RabbitMQ.Config;
    using Transports.RabbitMQ.Routing;

    public class RabbitMqTransport : ConfigureTransport<RabbitMQ>
    {
        public override void Initialize(Configure config)
        {
            if (!config.Settings.GetOrDefault<bool>("ScaleOut.UseSingleBrokerQueue"))
            {
                Address.InitializeLocalAddress(Address.Local.Queue + "." + Address.Local.Machine);
            }

            var connectionString = config.Settings.Get<string>("NServiceBus.Transport.ConnectionString");
            var connectionConfiguration = new ConnectionStringParser(config.Settings).Parse(connectionString);

            var configurer = config.Configurer;
            configurer.RegisterSingleton<IConnectionConfiguration>(connectionConfiguration);

            configurer.ConfigureComponent<RabbitMqDequeueStrategy>(DependencyLifecycle.InstancePerCall)
                 .ConfigureProperty(p => p.PurgeOnStartup, ConfigurePurging.PurgeRequested)
                 .ConfigureProperty(p => p.PrefetchCount, connectionConfiguration.PrefetchCount);

            configurer.ConfigureComponent<RabbitMqUnitOfWork>(DependencyLifecycle.InstancePerCall)
                  .ConfigureProperty(p => p.UsePublisherConfirms, connectionConfiguration.UsePublisherConfirms)
                  .ConfigureProperty(p => p.MaxWaitTimeForConfirms, connectionConfiguration.MaxWaitTimeForConfirms);


            configurer.ConfigureComponent<RabbitMqMessageSender>(DependencyLifecycle.InstancePerCall);

            configurer.ConfigureComponent<RabbitMqMessagePublisher>(DependencyLifecycle.InstancePerCall);

            configurer.ConfigureComponent<RabbitMqSubscriptionManager>(DependencyLifecycle.SingleInstance)
             .ConfigureProperty(p => p.EndpointQueueName, Address.Local.Queue);

            configurer.ConfigureComponent<RabbitMqQueueCreator>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(t=>t.UseDurableQueues,config.Settings.Get<bool>("Endpoint.DurableMessages"));


            if (config.Settings.HasSetting<IRoutingTopology>())
            {
                configurer.RegisterSingleton<IRoutingTopology>(config.Settings.Get <IRoutingTopology>());
            }
            else
            {
                configurer.ConfigureComponent<ConventionalRoutingTopology>(DependencyLifecycle.SingleInstance);
            }


            if (config.Settings.HasSetting("IManageRabbitMqConnections"))
            {
                configurer.ConfigureComponent(config.Settings.Get<Type>("IManageRabbitMqConnections"), DependencyLifecycle.SingleInstance);
            }
            else
            {
                configurer.ConfigureComponent<RabbitMqConnectionManager>(DependencyLifecycle.SingleInstance);

                configurer.ConfigureComponent<IConnectionFactory>(builder => new ConnectionFactoryWrapper(builder.Build<IConnectionConfiguration>(), new DefaultClusterHostSelectionStrategy<ConnectionFactoryInfo>()), DependencyLifecycle.InstancePerCall);

            }
        }


        protected override void InternalConfigure(Configure config)
        {
            Enable<RabbitMqTransport>();
        }

        protected override string ExampleConnectionStringForErrorMessage
        {
            get { return "host=localhost"; }
        }
    }
}