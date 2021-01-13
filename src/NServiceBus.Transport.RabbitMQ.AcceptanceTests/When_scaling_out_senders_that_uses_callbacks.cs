namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Configuration.AdvancedExtensibility;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Settings;

    public class When_scaling_out_senders_that_uses_callbacks : NServiceBusAcceptanceTest
    {
        const int numMessagesToSend = 5;

        [Test]
        public async Task Should_only_deliver_response_to_one_of_the_instances()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<ServerThatRespondsToCallbacks>()
                .WithEndpoint<ScaledOutClient>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.MakeInstanceUniquelyAddressable("A");
                        c.GetSettings().Set("Client", "A");
                    });
                    b.When(async (bus, c) =>
                    {
                        for (var i = 0; i < numMessagesToSend; i++)
                        {
                            var sendOptions = new SendOptions();
                            sendOptions.RouteReplyToThisInstance();

                            var myRequest = new MyRequest { Client = "A" };

                            await bus.Send(myRequest, sendOptions);
                        }
                    });
                })
                .WithEndpoint<ScaledOutClient>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.MakeInstanceUniquelyAddressable("B");
                        c.GetSettings().Set("Client", "B");
                    });
                    b.When(async (bus, c) =>
                    {
                        for (var i = 0; i < numMessagesToSend; i++)
                        {
                            var sendOptions = new SendOptions();
                            sendOptions.RouteReplyToThisInstance();

                            var myRequest = new MyRequest { Client = "B" };

                            await bus.Send(myRequest, sendOptions);
                        }
                    });
                })
                .Done(c => c.RepliesReceived >= numMessagesToSend * 2)
                .Run();

            Assert.AreEqual(2 * numMessagesToSend, context.RepliesReceived);
        }

        public class ScaledOutClient : EndpointConfigurationBuilder
        {
            public ScaledOutClient()
            {
                EndpointSetup<DefaultServer>(config =>
                    config.ConfigureTransport().Routing().RouteToEndpoint(typeof(MyRequest), typeof(ServerThatRespondsToCallbacks)));
            }

            class MyResponseHandler : IHandleMessages<MyResponse>
            {
                ReadOnlySettings settings;
                Context testContext;

                public MyResponseHandler(ReadOnlySettings settings, Context testContext)
                {
                    this.settings = settings;
                    this.testContext = testContext;
                }

                public Task Handle(MyResponse message, IMessageHandlerContext context)
                {
                    if (settings.Get<string>("Client") != message.Client)
                    {
                        throw new Exception("Wrong endpoint got the response.");
                    }
                    testContext.ReplyReceived();
                    return Task.CompletedTask;
                }
            }
        }

        public class ServerThatRespondsToCallbacks : EndpointConfigurationBuilder
        {
            public ServerThatRespondsToCallbacks()
            {
                EndpointSetup<DefaultServer>();
            }

            class MyRequestHandler : IHandleMessages<MyRequest>
            {
                public Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    var myResponse = new MyResponse { Client = message.Client };

                    return context.Reply(myResponse);
                }
            }
        }

        public class MyRequest : IMessage
        {
            public string Client { get; set; }
        }

        public class MyResponse : IMessage
        {
            public string Client { get; set; }
        }

        class Context : ScenarioContext
        {
            int repliesReceived;

            public int RepliesReceived => repliesReceived;

            public void ReplyReceived()
            {
                Interlocked.Increment(ref repliesReceived);
            }
        }
    }
}