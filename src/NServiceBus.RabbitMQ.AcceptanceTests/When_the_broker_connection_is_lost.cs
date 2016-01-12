namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Extensibility;
    using NServiceBus.Performance.TimeToBeReceived;
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

                public Task Start(IBusSession context)
                {
                    BreakConnectionBySendingInvalidMessage();

                    return context.SendLocal(new MyRequest
                    {
                        MessageId = myContext.MessageId
                    });
                }

                void BreakConnectionBySendingInvalidMessage()
                {
                    try
                    {
                        sender.Dispatch(new TransportOperations(new List<MulticastTransportOperation>(), new List<UnicastTransportOperation>
                        {
                            new UnicastTransportOperation(new OutgoingMessage("Foo", new Dictionary<string, string>(), new byte[0]), settings.EndpointName().ToString(), new List<DeliveryConstraint>
                            {
                                new DiscardIfNotReceivedBefore(TimeSpan.FromMilliseconds(-1))
                            })
                        }), new ContextBag());
                    }
                    catch (Exception)
                    {
                        // Don't care
                    }
                }

                public Task Stop(IBusSession context)
                {
                    return context.Completed();
                }
            }

            class MyHandler : IHandleMessages<MyRequest>
            {
                private readonly MyContext myContext;

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