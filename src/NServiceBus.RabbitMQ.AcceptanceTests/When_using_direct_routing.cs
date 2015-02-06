namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_direct_routing
    {
        [Test]
        public void Should_receive_the_message()
        {
            var context = new Context();

            Scenario.Define(context)
                   .WithEndpoint<Receiver>(b => b.Given((bus, c) => bus.SendLocal(new MyRequest())))
                   .Done(c => context.GotTheMessage)
                   .Run();

            Assert.True(context.GotTheMessage, "Should receive the message");
        }


        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c=> c.UseTransport<RabbitMQTransport>()
                    .UseDirectRoutingTopology());
            }

            class MyEventHandler : IHandleMessages<MyRequest>
            {
                public Context Context { get; set; }

                public void Handle(MyRequest message)
                {
                    Context.GotTheMessage = true;
                }
            }
        }

        class MyRequest : IMessage
        {
        }

        class Context : ScenarioContext
        {
            public bool GotTheMessage { get; set; }
        }
    }
}