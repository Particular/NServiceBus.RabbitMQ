namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::RabbitMQ.Client.Events;
    using NServiceBus.AcceptanceTesting.EndpointTemplates;
    using NUnit.Framework;

    class When_requesting_non_persistent_delivery : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_set_delivery_mode_accordingly()
        {
            var scenario = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) =>
                {
                    var options = new SendOptions();

                    options.RouteToThisEndpoint();
                    options.UseNonPersistentDeliveryMode();

                    return bus.Send(new Message(), options);
                }))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.False(scenario.DeliveryModeWasPersistent);
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
                    testContext.DeliveryModeWasPersistent = context.Extensions.Get<BasicDeliverEventArgs>().BasicProperties.Persistent;
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

            public bool DeliveryModeWasPersistent { get; set; }
        }
    }
}