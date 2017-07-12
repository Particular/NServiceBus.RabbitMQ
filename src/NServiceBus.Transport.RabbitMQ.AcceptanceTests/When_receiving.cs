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
            public async Task Then_the_handler_should_have_access_to_BasicDeliverEventArgs()
            {
                var scenario = await Scenario.Define<MyScenario>()
                    .WithEndpoint<Propagating>(b => b.When((bus, c) => bus.SendLocal(new Message())))
                    .Done(c => c.MessageReceived)
                    .Run();

                Assert.True(scenario.HandlerHasAccessToBasicDeliverEventArgs, "The handler should have access to BasicDeliverEventArgs");
            }
        }

        class And_the_endpoint_does_not_propagate_BasicDeliverEventArgs : NServiceBusAcceptanceTest
        {
            [Test]
            public async Task Then_the_handler_should_not_have_access_to_BasicDeliverEventArgs()
            {
                var scenario = await Scenario.Define<MyScenario>()
                    .WithEndpoint<NonPropagating>(b => b.When((bus, c) => bus.SendLocal(new Message())))
                    .Done(c => c.MessageReceived)
                    .Run();

                Assert.False(scenario.HandlerHasAccessToBasicDeliverEventArgs, "The handler should not have access to BasicDeliverEventArgs");
            }
        }

        public class Propagating : EndpointConfigurationBuilder
        {
            public Propagating()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<RabbitMQTransport>().PropagateBasicDeliverEventArgs(true));
            }
        }

        public class NonPropagating : EndpointConfigurationBuilder
        {
            public NonPropagating()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<RabbitMQTransport>().PropagateBasicDeliverEventArgs(false));
            }
        }

        class MyEventHandler : IHandleMessages<Message>
        {
            readonly MyScenario scenario;

            public MyEventHandler(MyScenario scenario)
            {
                this.scenario = scenario;
            }

            public Task Handle(Message message, IMessageHandlerContext context)
            {
                scenario.MessageReceived = true;

                try
                {
                    context.GetBasicDeliverEventArgs();
                }
                catch (InvalidOperationException)
                {
                    scenario.HandlerHasAccessToBasicDeliverEventArgs = false;
                    return TaskEx.CompletedTask;
                }

                scenario.HandlerHasAccessToBasicDeliverEventArgs = true;
                return TaskEx.CompletedTask;
            }
        }

        public class Message : IMessage
        {
        }

        class MyScenario : ScenarioContext
        {
            public bool MessageReceived { get; set; }

            public bool HandlerHasAccessToBasicDeliverEventArgs { get; set; }
        }
    }
}
