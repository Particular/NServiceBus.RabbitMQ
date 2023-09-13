namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.AcceptanceTesting.EndpointTemplates;
    using NUnit.Framework;

    class When_receiving_a_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_have_access_to_BasicDeliverEventArgs()
        {
            var scenario = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new Message())))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.True(scenario.HandlerHasAccessToBasicDeliverEventArgs, "The handler should have access to BasicDeliverEventArgs");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
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
                    testContext.HandlerHasAccessToBasicDeliverEventArgs = context.Extensions.TryGet<BasicDeliverEventArgs>(out _);
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

            public bool HandlerHasAccessToBasicDeliverEventArgs { get; set; }
        }
    }
}