namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
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
                EndpointSetup<DefaultServer>(c => c.UseTransport<RabbitMQTransport>());
            }

            class MyEventHandler : IHandleMessages<Message>
            {
                public Context Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    Context.HandlerHasAccessToBasicDeliverEventArgs = context.Extensions.TryGet<BasicDeliverEventArgs>(out _);
                    Context.MessageReceived = true;

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