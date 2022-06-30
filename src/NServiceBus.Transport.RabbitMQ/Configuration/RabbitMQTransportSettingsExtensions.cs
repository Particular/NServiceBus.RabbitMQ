namespace NServiceBus
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using Configuration.AdvancedExtensibility;
    using RabbitMQ.Client.Events;
    using Transport.RabbitMQ;

    /// <summary>
    /// Adds access to the RabbitMQ transport config to the global Transports object.
    /// </summary>
    public static partial class RabbitMQTransportSettingsExtensions
    {
        /// <summary>
        /// Registers a custom routing topology.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="topologyFactory">The function used to create the routing topology instance. The parameter of the function indicates whether exchanges and queues declared by the routing topology should be durable.</param>
        public static TransportExtensions<RabbitMQTransport> UseCustomRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<bool, IRoutingTopology> topologyFactory)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNull(nameof(topologyFactory), topologyFactory);

            transportExtensions.GetSettings().Set(topologyFactory);

            return transportExtensions;
        }

        /// <summary>
        /// Use the conventional routing topology. This is the preferred setting for new projects.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="queueType">The type of queue that the endpoint should use.</param>
        public static TransportExtensions<RabbitMQTransport> UseConventionalRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, QueueType queueType)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            return transportExtensions.UseCustomRoutingTopology(durable => new ConventionalRoutingTopology(durable, queueType));
        }

        /// <summary>
        /// Uses the direct routing topology with the specified conventions.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="queueType">The type of queue that the endpoint should use.</param>
        /// <param name="routingKeyConvention">The routing key convention.</param>
        /// <param name="exchangeNameConvention">The exchange name convention.</param>
        public static TransportExtensions<RabbitMQTransport> UseDirectRoutingTopology(this TransportExtensions<RabbitMQTransport> transportExtensions, QueueType queueType, Func<Type, string> routingKeyConvention = null, Func<string> exchangeNameConvention = null)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            if (routingKeyConvention == null)
            {
                routingKeyConvention = DefaultRoutingKeyConvention.GenerateRoutingKey;
            }

            if (exchangeNameConvention == null)
            {
                exchangeNameConvention = () => "amq.topic";
            }

            return transportExtensions.UseCustomRoutingTopology(durable => new DirectRoutingTopology(new DirectRoutingTopology.Conventions(exchangeNameConvention, routingKeyConvention), durable, queueType));
        }

        /// <summary>
        /// Allows the user to control how the message ID is determined. Mostly useful when consuming native messages from non-NServiceBus endpoints.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="customIdStrategy">The user-defined strategy for giving the message a unique ID.</param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> CustomMessageIdStrategy(this TransportExtensions<RabbitMQTransport> transportExtensions, Func<BasicDeliverEventArgs, string> customIdStrategy)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNull(nameof(customIdStrategy), customIdStrategy);

            transportExtensions.GetSettings().Set(SettingsKeys.CustomMessageIdStrategy, customIdStrategy);

            return transportExtensions;
        }

        /// <summary>
        /// When the enpdoint cannot communicate with the broker, set how long to wait before triggering the endpoint shutdown procedure.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="waitTime">The time to wait before triggering the circuit breaker.</param>
        public static TransportExtensions<RabbitMQTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan waitTime)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNegativeAndZero(nameof(waitTime), waitTime);

            transportExtensions.GetSettings().Set(SettingsKeys.TimeToWaitBeforeTriggeringCircuitBreaker, waitTime);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies the multiplier to apply to the maximum concurrency value to calculate the prefetch count. The default value is 3.
        /// Higher numbers will cause more messages to be received by the endpoint in a batch, but this is only valuable when message processing
        /// times are so short that latency of fetching new messages from the server is the limiting factor.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="prefetchMultiplier">The multiplier value to use in the prefetch calculation. The default value is 3.</param>
        public static TransportExtensions<RabbitMQTransport> PrefetchMultiplier(this TransportExtensions<RabbitMQTransport> transportExtensions, int prefetchMultiplier)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNegativeAndZero(nameof(prefetchMultiplier), prefetchMultiplier);

            transportExtensions.GetSettings().Set(SettingsKeys.PrefetchMultiplier, prefetchMultiplier);

            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default prefetch count calculation with the specified value.
        /// Higher numbers will cause more messages to be received by the endpoint in a batch, but this is only valuable when message processing
        /// times are so short that latency of fetching new messages from the server is the limiting factor.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="prefetchCount">The prefetch count to use.</param>
        public static TransportExtensions<RabbitMQTransport> PrefetchCount(this TransportExtensions<RabbitMQTransport> transportExtensions, ushort prefetchCount)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.GetSettings().Set(SettingsKeys.PrefetchCount, prefetchCount);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies the certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="clientCertificate">The certificate to use for client authentication.</param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> SetClientCertificate(this TransportExtensions<RabbitMQTransport> transportExtensions, X509Certificate2 clientCertificate)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNull(nameof(clientCertificate), clientCertificate);

            transportExtensions.GetSettings().Set(SettingsKeys.ClientCertificateCollection, new X509Certificate2Collection(clientCertificate));

            return transportExtensions;
        }

        /// <summary>
        /// Specifies the certificate to use for client authentication when connecting to the broker via TLS.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="path">The path to the certificate file.</param>
        /// <param name="password">The password for the certificate specified in <paramref name="path"/>.</param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> SetClientCertificate(this TransportExtensions<RabbitMQTransport> transportExtensions, string path, string password)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNullAndEmpty(nameof(path), path);
            Guard.AgainstNullAndEmpty(nameof(password), password);

            transportExtensions.GetSettings().Set(SettingsKeys.ClientCertificateCollection, new X509Certificate2Collection(new X509Certificate2(path, password)));

            return transportExtensions;
        }

        /// <summary>
        /// Disables all remote certificate validation when connecting to the broker via TLS.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> DisableRemoteCertificateValidation(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.GetSettings().Set(SettingsKeys.DisableRemoteCertificateValidation, true);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies that an external authentication mechanism should be used for client authentication.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> UseExternalAuthMechanism(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.GetSettings().Set(SettingsKeys.UseExternalAuthMechanism, true);

            return transportExtensions;
        }

        /// <summary>
        /// Gets the delayed delivery settings.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <returns></returns>
        public static DelayedDeliverySettings DelayedDelivery(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            return new DelayedDeliverySettings(transportExtensions.GetSettings());
        }

        /// <summary>
        /// Specifies that exchanges and queues should be declared as non-durable.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> DisableDurableExchangesAndQueues(this TransportExtensions<RabbitMQTransport> transportExtensions)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            transportExtensions.GetSettings().Set(SettingsKeys.UseDurableExchangesAndQueues, false);

            return transportExtensions;
        }

        /// <summary>
        /// Sets the interval for heartbeats between the endpoint and the broker.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="heartbeatInterval">The time interval to use.</param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> SetHeartbeatInterval(this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan heartbeatInterval)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNegativeAndZero(nameof(heartbeatInterval), heartbeatInterval);

            transportExtensions.GetSettings().Set(SettingsKeys.HeartbeatInterval, heartbeatInterval);

            return transportExtensions;
        }

        /// <summary>
        /// Sets the time to wait between attempts to reconnect to the broker if the connection is lost.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="networkRecoveryInterval">The time interval to use.</param>
        /// <returns></returns>
        public static TransportExtensions<RabbitMQTransport> SetNetworkRecoveryInterval(this TransportExtensions<RabbitMQTransport> transportExtensions, TimeSpan networkRecoveryInterval)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNegativeAndZero(nameof(networkRecoveryInterval), networkRecoveryInterval);

            transportExtensions.GetSettings().Set(SettingsKeys.NetworkRecoveryInterval, networkRecoveryInterval);

            return transportExtensions;
        }
    }
}