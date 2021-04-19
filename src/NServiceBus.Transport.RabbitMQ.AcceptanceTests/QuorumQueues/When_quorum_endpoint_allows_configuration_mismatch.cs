namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_quorum_endpoint_allows_configuration_mismatch : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_allow_quorum_endpoint_to_connect_to_classic_queue()
        {
            await Scenario.Define<ScenarioContext>()
                .WithEndpoint<ClassicQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<QuorumQueueEndpoint>(b => b
                    .CustomConfig(c => c
                        .ConfigureRabbitMQTransport().AllowInputQueueConfigurationMismatch = true)
                    .When(s => s.SendLocal(new TestMessage())))
                .Done(c => c.ReceivedMessage)
                .Run();

            Assert.IsTrue(context.ReceivedMessage);
        }

        class Context : ScenarioContext
        {
            public bool ReceivedMessage { get; set; }
        }

        class ClassicQueueEndpoint : EndpointConfigurationBuilder
        {
            public ClassicQueueEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.OverrideLocalAddress(Conventions.EndpointNamingConvention(typeof(QuorumQueueEndpoint)));
                });
            }
        }

        class QuorumQueueEndpoint : EndpointConfigurationBuilder
        {
            public QuorumQueueEndpoint()
            {
                var clusterTemplate = new ClusterEndpoint(QueueMode.Quorum);
                EndpointSetup(clusterTemplate, (c, __) =>
                {
                    c.SendFailedMessagesTo("quorum-error");
                }, _ => { });
            }

            public class TestMessageHandler : IHandleMessages<TestMessage>
            {
                Context testContext;

                public TestMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(TestMessage message, IMessageHandlerContext context)
                {
                    testContext.ReceivedMessage = true;
                    return Task.CompletedTask;
                }
            }
        }

        class TestMessage : IMessage
        {
        }
    }
}