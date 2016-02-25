namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Extensibility;
    using NServiceBus.Performance.TimeToBeReceived;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NUnit.Framework;

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
                EndpointSetup<DefaultServer>();
            }

            class ConnectionKiller : IWantToRunWhenBusStartsAndStops
            {
                readonly IDispatchMessages sender;
                readonly ReadOnlySettings settings;
                readonly MyContext myContext;

                public ConnectionKiller(IDispatchMessages sender, ReadOnlySettings settings, MyContext myContext)
                {
                    this.sender = sender;
                    this.settings = settings;
                    this.myContext = myContext;
                }

                public async Task Start(IMessageSession context)
                {
                    await BreakConnectionBySendingInvalidMessage();

                    await context.SendLocal(new MyRequest { MessageId = myContext.MessageId });
                }

                async Task BreakConnectionBySendingInvalidMessage()
                {
                    try
                    {
                        var outgoingMessage = new OutgoingMessage("Foo", new Dictionary<string, string>(), new byte[0]);
                        var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(settings.EndpointName().ToString()), deliveryConstraints: new [] { new DiscardIfNotReceivedBefore(TimeSpan.FromMilliseconds(-1)) });
                        await sender.Dispatch(new TransportOperations(operation), new ContextBag());
                    }
                    catch (Exception)
                    {
                        // Don't care
                    }
                }

                public Task Stop(IMessageSession context)
                {
                    return context.Completed();
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

                    return context.Completed();
                }
            }
        }

        class MyRequest : IMessage
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