namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using System.Threading.Tasks;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;

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
                        c.ScaleOut().InstanceDiscriminator("A");
                        c.GetSettings().Set("Client", "A");
                    });
                    b.When(async (bus, c) =>
                    {
                        for (var i = 0; i < numMessagesToSend; i++)
                        {
                            var sendOptions = new SendOptions();
                            sendOptions.RouteReplyToThisInstance();
                            await bus.Send(new MyRequest()
                            {
                                Client = "A"
                            }, sendOptions);
                        }
                    });
                })
                .WithEndpoint<ScaledOutClient>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.ScaleOut().InstanceDiscriminator("B");
                        c.GetSettings().Set("Client", "B");
                    });
                    b.When(async (bus, c) =>
                    {
                        for (var i = 0; i < numMessagesToSend; i++)
                        {
                            var sendOptions = new SendOptions();
                            sendOptions.RouteReplyToThisInstance();
                            await bus.Send(new MyRequest()
                            {
                                Client = "B"
                            }, sendOptions);
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
                EndpointSetup<DefaultServer>()
                    .AddMapping<MyRequest>(typeof(ServerThatRespondsToCallbacks));
            }

            class MyResponseHandler : IHandleMessages<MyReposnse>
            {
                public ReadOnlySettings Settings { get; set; }

                public Context Context { get; set; }

                public Task Handle(MyReposnse message, IMessageHandlerContext context)
                {
                    if (Settings.Get<string>("Client") != message.Client)
                    {
                        throw new Exception("Wrong endpoint got the response.");
                    }
                    Context.ReplyReceived();
                    return context.Completed();
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
                public async Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    await context.Reply(new MyReposnse()
                    {
                        Client = message.Client
                    });
                }
            }
        }

        class MyRequest : IMessage
        {
            public string Client { get; set; }
        }

        class MyReposnse : IMessage
        {
            public string Client { get; set; }
        }

        class Context : ScenarioContext
        {
            int repliesReceived;

            public int RepliesReceived
            {
                get { return repliesReceived; }
            }

            public void ReplyReceived()
            {
                Interlocked.Increment(ref repliesReceived);
            }
        }
    }
}