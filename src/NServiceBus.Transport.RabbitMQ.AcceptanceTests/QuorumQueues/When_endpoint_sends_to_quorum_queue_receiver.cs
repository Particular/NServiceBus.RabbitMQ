namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using global::RabbitMQ.Client;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_endpoint_sends_to_quorum_queue_receiver : NServiceBusAcceptanceTest
    {
        const string DestinationQueueName = "rabbitmq.transport.tests.quorum-destination";

        [Test]
        public async Task Should_not_fail_to_send()
        {
            using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.DeclareQuorumQueue(DestinationQueueName);
                channel.ExchangeDeclare(DestinationQueueName, ExchangeType.Fanout, true);
                channel.QueueBind(DestinationQueueName, DestinationQueueName, string.Empty);
            }

            var context = await Scenario.Define<ScenarioContext>()
                .WithEndpoint<ClassicQueueEndpoint>(e => e.When(async s =>
                {
                    var options = new SendOptions();
                    options.SetDestination(DestinationQueueName);
                    await s.Send(new MessageToQuorumReceiver(), options);
                }))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.IsTrue(context.EndpointsStarted);
        }

        class ClassicQueueEndpoint : EndpointConfigurationBuilder
        {
            public ClassicQueueEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }
        }

        class MessageToQuorumReceiver : IMessage
        {
        }
    }
}