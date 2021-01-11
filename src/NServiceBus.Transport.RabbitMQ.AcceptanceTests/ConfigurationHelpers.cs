using NServiceBus;
using NServiceBus.AcceptanceTests.EndpointTemplates;

static class ConfigurationHelpers
{
    public static RabbitMQTransport ConfigureRabbitMQTransport(this EndpointConfiguration configuration)
    {
        return (RabbitMQTransport) configuration.ConfigureTransport();
    }
}