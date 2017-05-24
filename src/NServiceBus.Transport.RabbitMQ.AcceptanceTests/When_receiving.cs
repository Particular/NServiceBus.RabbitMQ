namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_receiving
    {
        class And_the_endpoint_propagates_BasicDeliverEventArgs : NServiceBusAcceptanceTest
        {
            [Test]
            public async Task Should_have_access_to_BasicDeliverEventArgs()
            {
                var context = await Scenario.Define<MyContext>()
                    .WithEndpoint<PropagatingReceiver>(b => b.When((bus, c) => bus.SendLocal(new MyRequest())))
                    .Done(c => c.MessageReceived)
                    .Run();

                Assert.True(context.GotTheArgs, "Should have access to BasicDeliverEventArgs");
            }
        }

        class And_the_endpoint_does_not_propagate_BasicDeliverEventArgs : NServiceBusAcceptanceTest
        {
            [Test]
            public async Task Should_not_have_access_to_BasicDeliverEventArgs()
            {
                var context = await Scenario.Define<MyContext>()
                    .WithEndpoint<NonPropagatingReceiver>(b => b.When((bus, c) => bus.SendLocal(new MyRequest())))
                    .Done(c => c.MessageReceived)
                    .Run();

                Assert.False(context.GotTheArgs, "Should not have access to BasicDeliverEventArgs");
            }
        }

        public class PropagatingReceiver : EndpointConfigurationBuilder
        {
            public PropagatingReceiver()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<RabbitMQTransport>().PropagateBasicDeliverEventArgs(true));
            }
        }

        public class NonPropagatingReceiver : EndpointConfigurationBuilder
        {
            public NonPropagatingReceiver()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<RabbitMQTransport>().PropagateBasicDeliverEventArgs(false));
            }
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
                myContext.MessageReceived = true;

                try
                {
                    context.GetBasicDeliverEventArgs();
                }
                catch (InvalidOperationException)
                {
                    myContext.GotTheArgs = false;
                    return TaskEx.CompletedTask;
                }

                myContext.GotTheArgs = true;
                return TaskEx.CompletedTask;
            }
        }

        public class MyRequest : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool MessageReceived { get; set; }

            public bool GotTheArgs { get; set; }
        }
    }
}
