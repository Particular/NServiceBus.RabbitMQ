namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using System.Threading.Tasks;

    public class When_scaling_out_senders_that_uses_callbacks : NServiceBusAcceptanceTest
    {
        const int numMessagesToSend = 5;

        [Test]
        public async Task Should_only_deliver_response_to_one_of_the_instances()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<ServerThatRespondsToCallbacks>(b =>
                {
                    b.CustomConfig(c => c.ScaleOut().InstanceDiscriminator("A"));
                })
                .WithEndpoint<ScaledOutClient>(b =>
                {
                    b.CustomConfig(c => c.ScaleOut().InstanceDiscriminator("A"));
                    b.When(async (bus, c) =>
                    {
                        for (var i = 0; i < numMessagesToSend; i++)
                        {
                            var sendOptions = new SendOptions();
                            var response = await bus.Request<int>(new MyRequest { ReturnCode = 1 }, sendOptions);

                            if (response != 1)
                            {
                                throw new Exception("Wrong server got the response");
                            }

                            c.ServerAGotTheCallback++;
                        }
                    });
                })
                .WithEndpoint<ScaledOutClient>(b =>
                {
                    b.CustomConfig(c => c.ScaleOut().InstanceDiscriminator("B"));
                    b.When(async (bus, c) =>
                    {
                        for (var i = 0; i < numMessagesToSend; i++)
                        {
                            var sendOptions = new SendOptions();
                            var response = await bus.Request<int>(new MyRequest { ReturnCode = 2 }, sendOptions);

                            if (response != 2)
                            {
                                throw new Exception("Wrong server got the response");
                            }

                            c.ServerBGotTheCallback++;
                        }
                    });
                })
                .Done(c => (c.ServerAGotTheCallback + c.ServerBGotTheCallback) >= numMessagesToSend * 2)
                .Run();

            Assert.AreEqual(numMessagesToSend, context.ServerAGotTheCallback, "Both scaled out instances should get the callback");

            Assert.AreEqual(numMessagesToSend, context.ServerBGotTheCallback, "Both scaled out instances should get the callback");
        }

        public class ScaledOutClient : EndpointConfigurationBuilder
        {
            public ScaledOutClient()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<MyRequest>(typeof(ServerThatRespondsToCallbacks));
            }
        }

        public class ServerThatRespondsToCallbacks : EndpointConfigurationBuilder
        {
            public ServerThatRespondsToCallbacks()
            {
                EndpointSetup<DefaultServer>();
            }

            class MyEventHandler : IHandleMessages<MyRequest>
            {
                public async Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    await context.Reply(message.ReturnCode);
                }
            }
        }

        class MyRequest : IMessage
        {
            public int ReturnCode { get; set; }
        }

        class Context : ScenarioContext
        {
            public int ServerAGotTheCallback { get; set; }
            public int ServerBGotTheCallback { get; set; }
        }
    }
}