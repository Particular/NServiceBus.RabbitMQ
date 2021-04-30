namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_classic_endpoint_allows_configuration_mismatch : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_allow_classic_endpoint_connect_to_quorum_queue()
        {
            // Create/verify input queue as quorum queue:
            using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.DeclareQuorumQueue(Conventions.EndpointNamingConvention(typeof(ClassicQueueEndpoint)));
            }

            var context = await Scenario.Define<Context>()
                .WithEndpoint<ClassicQueueEndpoint>(b => b
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
                EndpointSetup<DefaultServer>();
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