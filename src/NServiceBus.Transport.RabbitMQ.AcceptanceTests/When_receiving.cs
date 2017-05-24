namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_receiving : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_have_access_to_Rabbit_args()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new MyRequest())))
                .Done(c => c.GotTheArgs)
                .Run();

            Assert.True(context.GotTheArgs, "Should receive the Args");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<RabbitMQTransport>());
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
                    myContext.GotTheArgs = context.GetBasicDeliverEventArgs() != null;
                    return TaskEx.CompletedTask;
                }
            }
        }

        public class MyRequest : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool GotTheArgs { get; set; }
        }
    }
}
