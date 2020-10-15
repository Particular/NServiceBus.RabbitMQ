using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace NServiceBus
{
    using Settings;
    using Transport.RabbitMQ;
    using Transport;

    /// <summary>
    /// Transport definition for RabbitMQ.
    /// </summary>
    public class RabbitMQTransport : TransportDefinition
    {
        /// <summary>
        /// 
        /// </summary>
        public RabbitMQTransport(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// 
        /// </summary>
        public X509Certificate2Collection X509Certificate2Collection { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool DisableRemoteCertificateValidation { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool UseExternalAuthMechanism { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public TimeSpan? HeartbeatInterval { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public TimeSpan? NetworkRecoveryInterval { get; set; }

        internal Func<bool, IRoutingTopology> TopologyFactory { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool UseDurableExchangesAndQueues { get; set; } = true;

        /// <summary>
        ///
        /// </summary>
        public TimeSpan TimeToWaitBeforeTriggeringCircuitBreaker { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// 
        /// </summary>
        public int PrefetchMultiplier { get; set; } = 3;

        /// <summary>
        /// 
        /// </summary>
        public ushort PrefetchCount { get; set; } = 0;

        /// <summary>
        /// 
        /// </summary>
        public Func<BasicDeliverEventArgs, string> CustomMessageIdStrategy { get; set; }

        /// <summary>
        /// Initializes all the factories and supported features for the transport.
        /// </summary>
        public override Task<TransportInfrastructure> Initialize(Transport.Settings settings)
        {
            return Task.FromResult<TransportInfrastructure>(new RabbitMQTransportInfrastructure(settings, this));
        }
    }
}
