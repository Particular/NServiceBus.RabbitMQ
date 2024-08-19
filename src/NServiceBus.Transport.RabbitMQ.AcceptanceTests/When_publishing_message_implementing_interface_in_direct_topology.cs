namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using Logging;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_publishing_message_implementing_interface_in_direct_topology : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_log_a_warning()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<Publisher>(b =>
                        b.When(c => c.Subscriber1Subscribed, session => session.Publish(new MyRequest())))
                .WithEndpoint<Receiver>(b => b.When(async (session, c) =>
                {
                    await session.Subscribe<IMyRequest>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.Subscriber1Subscribed = true;
                    }
                }))
                .Done(c => c.GotTheMessage)
                .Run();

            Assert.That(context.Logs.Any(l => l.Level == LogLevel.Warn && l.Message.Contains("The direct routing topology cannot properly publish a message type that implements")), Is.True);
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureRabbitMQTransport().RoutingTopology = new DirectRoutingTopology(true, QueueType.Classic);
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureRabbitMQTransport().RoutingTopology = new DirectRoutingTopology(true, QueueType.Classic);
                    builder.DisableFeature<AutoSubscribe>();
                }, metadata => metadata.RegisterPublisherFor<IMyRequest>(typeof(Publisher)));
            }

            class MyEventHandler : IHandleMessages<IMyRequest>
            {
                readonly MyContext myContext;

                public MyEventHandler(MyContext myContext)
                {
                    this.myContext = myContext;
                }

                public Task Handle(IMyRequest message, IMessageHandlerContext context)
                {
                    myContext.GotTheMessage = true;

                    return Task.CompletedTask;
                }
            }
        }

        public class MyRequest : IMyRequest, IOtherRequest
        {
        }

        public interface IMyRequest : IEvent
        {
        }

        public interface IOtherRequest : IEvent
        {
        }

        class MyContext : ScenarioContext
        {
            public bool GotTheMessage { get; set; }
            public bool Subscriber1Subscribed { get; set; }
        }
    }
}