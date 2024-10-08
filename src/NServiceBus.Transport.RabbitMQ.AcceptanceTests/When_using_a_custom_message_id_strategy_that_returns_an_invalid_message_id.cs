﻿namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_a_custom_message_id_strategy_that_returns_an_invalid_message_id : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_still_receive_message_if_sent_from_NServiceBus()
        {
            var context = await Scenario.Define<MyContext>()
                   .WithEndpoint<Receiver>(c => c.When(session => session.SendLocal(new MyRequest())))
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
                    c.ConfigureRabbitMQTransport().MessageIdStrategy = m => "";
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