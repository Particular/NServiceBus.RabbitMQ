namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Transports.RabbitMQ;
    using NUnit.Framework;

    public class When_the_broker_connection_is_lost
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

            class ConnectionKiller:IWantToRunWhenBusStartsAndStops
            {
                readonly IManageRabbitMqConnections connectionManager;
                readonly MyContext myContext;

                public ConnectionKiller(IManageRabbitMqConnections connectionManager, MyContext myContext)
                {
                    this.connectionManager = connectionManager;
                    this.myContext = myContext;
                }

                public Task Start(IBusContext context)
                {
                    connectionManager.GetConsumeConnection().Abort();
                    return context.SendLocal(new MyRequest
                    {
                        MessageId = myContext.MessageId
                    });
                }

                public Task Stop(IBusContext context)
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