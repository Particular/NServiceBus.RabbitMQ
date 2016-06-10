namespace NServiceBus
{
    using Settings;
    using Transports;
    using Transport.RabbitMQ;

    /// <summary>
    /// Transport definition for RabbitMQ.
    /// </summary>
    public class RabbitMQTransport : TransportDefinition
    {
        /// <summary>
        /// Initializes all the factories and supported features for the transport.
        /// </summary>
        /// <param name="settings">An instance of the current settings.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <returns>The supported factories.</returns>
        protected override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString) => new RabbitMQTransportInfrastructure(settings, connectionString);

        /// <summary>
        /// Gets an example connection string to use when reporting the lack of a configured connection string to the user.
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage => "host=localhost";
    }
}
