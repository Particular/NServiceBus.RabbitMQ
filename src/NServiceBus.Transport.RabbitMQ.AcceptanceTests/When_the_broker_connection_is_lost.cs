namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
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
            var context = await Scenario.Define<MyContext>(ctx =>
            {
                ctx.MessageId = Guid.NewGuid().ToString();
            })
            .WithEndpoint<Receiver>()
            .Done(c => c.GotTheMessage)
            .Run();

            Assert.That(context.GotTheMessage, Is.True, "Should receive the message");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() =>
                EndpointSetup<DefaultServer>(e =>
                {
                    e.EnableFeature<ConnectionKillerFeature>();
                });

            class ConnectionKillerFeature : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.Services.AddTransient<ConnectionKiller>();
                    context.RegisterStartupTask(b => b.GetRequiredService<ConnectionKiller>());
                }

                class ConnectionKiller(IMessageDispatcher sender, IReadOnlySettings settings, MyContext context)
                    : FeatureStartupTask
                {
                    protected override async Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
                    {
                        await BreakConnectionBySendingInvalidMessage(cancellationToken);

                        await session.SendLocal(new MyRequest { MessageId = context.MessageId }, cancellationToken);
                    }

                    protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;

                    async Task BreakConnectionBySendingInvalidMessage(CancellationToken cancellationToken)
                    {
                        try
                        {
                            var outgoingMessage = new OutgoingMessage("Foo", [], Array.Empty<byte>());
                            var props = new DispatchProperties
                            {
                                DiscardIfNotReceivedBefore =
                                    new DiscardIfNotReceivedBefore(TimeSpan.FromMilliseconds(-1))
                            };
                            var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(settings.EndpointName()), props);
                            await sender.Dispatch(new TransportOperations(operation), new TransportTransaction(), cancellationToken);
                        }
                        catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
                        {
                            // Don't care
                        }
                    }
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