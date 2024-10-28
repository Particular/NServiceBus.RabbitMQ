namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    class When_customizing_outgoing_messages : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_set_value()
        {
            var scenario = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new Message())))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.That(scenario.BasicDeliverEventArgs.BasicProperties.AppId, Is.EqualTo("MyValue"));
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(endpointConfiguration =>
                {
                    endpointConfiguration.ConfigureRabbitMQTransport().OutgoingNativeMessageCustomization =
                        (operation, properties) =>
                        {
                            properties.AppId = "MyValue";
                        };
                });
            }

            class MyEventHandler : IHandleMessages<Message>
            {
                Context testContext;

                public MyEventHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    testContext.BasicDeliverEventArgs = context.Extensions.Get<BasicDeliverEventArgs>();
                    testContext.MessageReceived = true;

                    return Task.CompletedTask;
                }
            }
        }

        public class Message : IMessage
        {
        }

        class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }

            public BasicDeliverEventArgs BasicDeliverEventArgs { get; set; }
        }
    }
}