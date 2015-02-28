namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Transports.RabbitMQ;
    using NUnit.Framework;

    public class When_the_broker_connection_is_lost
    {
        [Test]
        public void Should_reconnect()
        {
            var context = new Context
            {
                MessageId = Guid.NewGuid().ToString()
            };

            Scenario.Define(context)
                   .WithEndpoint<Receiver>()
                   .Done(c => context.GotTheMessage)
                   .AllowExceptions()
                   .Run();

            Assert.True(context.GotTheMessage, "Should receive the message");
        }


        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            class ConnectionKiller:IWantToRunWhenBusStartsAndStops
            {
                readonly IManageRabbitMqConnections connectionManager;
                readonly IBus bus;
                readonly Context context;

                public ConnectionKiller(IManageRabbitMqConnections connectionManager,IBus bus,Context context)
                {
                    this.connectionManager = connectionManager;
                    this.bus = bus;
                    this.context = context;
                }

                public void Start()
                {
                    connectionManager.GetConsumeConnection().Abort();
                    bus.SendLocal(new MyRequest
                    {
                        MessageId = context.MessageId
                    });
                }

                public void Stop()
                {
                }
            }

            class MyHandler : IHandleMessages<MyRequest>
            {
                public Context Context { get; set; }

                public void Handle(MyRequest message)
                {
                    if (message.MessageId == Context.MessageId)
                    {
                        Context.GotTheMessage = true;             
                    }
                }
            }
        }

        class MyRequest : IMessage
        {
            public string MessageId { get; set; }
        }

        class Context : ScenarioContext
        {
            public bool GotTheMessage { get; set; }
            public string MessageId { get; set; }
        }
    }
}