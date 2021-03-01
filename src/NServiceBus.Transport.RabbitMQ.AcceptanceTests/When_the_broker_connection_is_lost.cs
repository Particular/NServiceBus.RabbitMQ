namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;

    public class When_the_broker_connection_is_lost : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_reconnect()
        {
            var context = await Scenario.Define<MyContext>(myContext =>
            {
                myContext.MessageId = Guid.NewGuid().ToString();
            })
                .WithEndpoint<Receiver>()
                .Done(c => c.GotTheMessage)
                .Run();

            Assert.True(context.GotTheMessage, "Should receive the message");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(e =>
                {
                    e.EnableFeature<ConnectionKillerFeature>();
                });
            }

            class ConnectionKillerFeature : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.Services.AddTransient<ConnectionKiller>();
                    context.RegisterStartupTask(b => b.GetRequiredService<ConnectionKiller>());
                }

                class ConnectionKiller : FeatureStartupTask
                {
                    public ConnectionKiller(IMessageDispatcher sender, ReadOnlySettings settings, MyContext context)
                    {
                        this.context = context;
                        this.sender = sender;
                        this.settings = settings;
                    }

                    protected override async Task OnStart(IMessageSession session, CancellationToken cancellationToken)
                    {
                        await BreakConnectionBySendingInvalidMessage();

                        await session.SendLocal(new MyRequest { MessageId = context.MessageId }, cancellationToken);
                    }

                    protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken) => Task.CompletedTask;

                    async Task BreakConnectionBySendingInvalidMessage()
                    {
                        try
                        {
                            var outgoingMessage = new OutgoingMessage("Foo", new Dictionary<string, string>(), new byte[0]);
                            var props = new DispatchProperties
                            {
                                DiscardIfNotReceivedBefore =
                                    new DiscardIfNotReceivedBefore(TimeSpan.FromMilliseconds(-1))
                            };
                            var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(settings.EndpointName()), props);
                            await sender.Dispatch(new TransportOperations(operation), new TransportTransaction(), CancellationToken.None);
                        }
                        catch (Exception)
                        {
                            // Don't care
                        }
                    }

                    readonly MyContext context;
                    readonly IMessageDispatcher sender;
                    readonly ReadOnlySettings settings;
                }
            }

            class MyHandler : IHandleMessages<MyRequest>
            {
                readonly MyContext myContext;

                public MyHandler(MyContext myContext)
                {
                    this.myContext = myContext;
                }

                public Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    if (message.MessageId == myContext.MessageId)
                    {
                        myContext.GotTheMessage = true;
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class MyRequest : IMessage
        {
            public string MessageId { get; set; }
        }

        class MyContext : ScenarioContext
        {
            public bool GotTheMessage { get; set; }
            public string MessageId { get; set; }
        }
    }
}