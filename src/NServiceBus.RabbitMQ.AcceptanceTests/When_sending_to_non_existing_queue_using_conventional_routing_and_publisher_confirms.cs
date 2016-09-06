namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_to_non_existing_queue_using_conventional_routing_and_publisher_confirms : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_throw_exception()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When(async (bus, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.SetDestination("nonExistingQueue");
                    try
                    {
                        await bus.Send(new MyRequest(), sendOptions);
                    }
                    catch (Exception ex)
                    {
                        c.Exception = ex;
                        c.GotTheException = true;
                    }
                }))
                .Done(c => c.GotTheException)
                .Run();
            
            Assert.IsInstanceOf<Exception>(context.Exception);
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<RabbitMQTransport>()
                    .UsePublisherConfirms(true));
            }
        }

        class MyRequest : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool GotTheException { get; set; }
            public Exception Exception { get; set; }
        }
    }
}