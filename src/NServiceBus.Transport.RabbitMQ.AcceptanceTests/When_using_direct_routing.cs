﻿namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_direct_routing : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_the_message()
        {
            var context = await Scenario.Define<MyContext>()
                   .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new MyRequest())))
                   .Done(c => c.GotTheMessage)
                   .Run();

            Assert.That(context.GotTheMessage, Is.True, "Should receive the message");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureRabbitMQTransport().RoutingTopology = new DirectRoutingTopology(true, QueueType.Classic);
                });
            }

            class MyEventHandler : IHandleMessages<MyRequest>
            {
                readonly MyContext myContext;

                public MyEventHandler(MyContext myContext)
                {
                    this.myContext = myContext;
                }

                public Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    myContext.GotTheMessage = true;

                    return Task.CompletedTask;
                }
            }
        }

        public class MyRequest : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool GotTheMessage { get; set; }
        }
    }
}