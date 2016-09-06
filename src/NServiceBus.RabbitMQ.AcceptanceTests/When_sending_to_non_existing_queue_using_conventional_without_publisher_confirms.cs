﻿namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_to_non_existing_queue_using_conventional_without_publisher_confirms : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Is_Losing_Messag()
        {
            var context = await Scenario.Define<ScenarioContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.SetDestination("nonExistingQueue");
                    return bus.Send(new MyRequest(), sendOptions);
                }))
                .Run(TimeSpan.FromSeconds(20));
            
            Assert.IsFalse(context.Logs.Any(x => x.Message.StartsWith("Message could not be routed to")));
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<RabbitMQTransport>().UsePublisherConfirms(false));
            }
        }

        class MyRequest : IMessage
        {
        }
    }
}