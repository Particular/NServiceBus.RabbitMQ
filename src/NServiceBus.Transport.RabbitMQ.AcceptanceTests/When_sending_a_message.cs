namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    class When_sending_a_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Route_Not_Found_Should_throw_a_meaningful_Exception()
        {
            var messageId = Guid.NewGuid().ToString();
            var exceptionMessage = "";
            try
            {
                await Scenario.Define<ScenarioContext>()
                    .WithEndpoint<Sender>(b => b.When((bus, c) =>
                    {
                        var options = new SendOptions();
                        options.SetMessageId(messageId);
                        options.SetDestination("NotExistingDestination");
                        return bus.Send(new Message(), options);
                    }))
                    .Run();
            }
            catch (Exception e)
            {
                exceptionMessage = e.Message;
            }
            Assert.That(exceptionMessage,
                Is.EqualTo($"Message {messageId} could not be routed to NotExistingDestination"),
                "Exception thrown does not match expected message");
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServer>();
        }

        class Message : IMessage
        {
        }
    }
}