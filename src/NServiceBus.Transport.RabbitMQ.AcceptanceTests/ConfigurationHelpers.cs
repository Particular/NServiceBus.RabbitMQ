using NServiceBus;
using NServiceBus.AcceptanceTesting.EndpointTemplates;

static class ConfigurationHelpers
{
    public static RabbitMQTransport ConfigureRabbitMQTransport(this EndpointConfiguration configuration)
    {
        return (RabbitMQTransport)configuration.ConfigureTransport();
    }
}