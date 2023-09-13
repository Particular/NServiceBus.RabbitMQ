namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests.DelayedDelivery
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.EndpointTemplates;
    using NUnit.Framework;

    public class When_deferring_a_message_longer_than_allowed_maximum : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw()
        {
            var delay = TimeSpan.FromDays(365 * 1000);

            var exception = Assert.ThrowsAsync<Exception>(() => Scenario.Define<ScenarioContext>()
                .WithEndpoint<Endpoint>(b => b.When((session, c) =>
                {
                    var options = new SendOptions();

                    options.DelayDeliveryWith(delay);
                    options.RouteToThisEndpoint();

                    return session.Send(new MyMessage(), options);
                }))
                .Done(context => !context.FailedMessages.IsEmpty)
                .Run());

            Assert.That(exception, Is.Not.Null);
            StringAssert.StartsWith("Message cannot be delayed by", exception.Message);
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => Task.CompletedTask;
            }
        }

        public class MyMessage : IMessage { }
    }
}