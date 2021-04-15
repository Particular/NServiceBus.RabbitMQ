namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_delayed_delivery_with_quorum_queues : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_not_allow_delayed_retries()
        {
            var exception = Assert.ThrowsAsync<Exception>(async () => await Scenario.Define<ScenarioContext>()
                .WithEndpoint<EndpointWithQuorumQueue>(b => b
                    .CustomConfig(config => config
                        .Recoverability().Delayed(d => d.NumberOfRetries(3))))
                .Done(c => c.EndpointsStarted)
                .Run());

            StringAssert.Contains("Delayed retries are not supported when the transport does not support delayed delivery.", exception.Message);
        }

        //TODO: when using delayed sends
        //TODO: when using saga timeouts

        public class EndpointWithQuorumQueue : EndpointConfigurationBuilder
        {
            public EndpointWithQuorumQueue()
            {
                var transportConfiguration = new ConfigureEndpointRabbitMQTransport(useQuorumQueue: true);
                var defaultServer = new DefaultServer
                {
                    TransportConfiguration = transportConfiguration
                };
                EndpointSetup(
                    defaultServer,
                    (configuration, r) =>
                    {
                        // need to configure a different error queue that isn't a classic queue.
                        configuration.SendFailedMessagesTo("error-quorum");
                    },
                    _ => { });
            }
        }
    }
}