﻿namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NUnit.Framework;

    public class When_using_a_custom_message_id_strategy : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_be_able_to_receive_messages_with_no_id()
        {
            var context = await Scenario.Define<MyContext>()
                   .WithEndpoint<Receiver>()
                   .Done(c => c.GotTheMessage)
                   .Run();

            Assert.True(context.GotTheMessage, "Should receive the message");
        }


        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<RabbitMQTransport>()
                    //just returning a guid here, not suitable for production use
                    .CustomMessageIdStrategy(m => Guid.NewGuid().ToString()));
            }

            class Starter : IWantToRunWhenBusStartsAndStops
            {
                readonly IDispatchMessages dispatchMessages;
                private readonly IMessageSerializer serializer;
                private readonly ReadOnlySettings settings;

                public Starter(IDispatchMessages dispatchMessages, IMessageSerializer serializer, ReadOnlySettings settings)
                {
                    this.dispatchMessages = dispatchMessages;
                    this.serializer = serializer;
                    this.settings = settings;
                }

                public async Task Start(IBusSession context)
                {
                    using (var stream = new MemoryStream())
                    {
                        serializer.Serialize(new MyRequest(), stream);

                        var message = new OutgoingMessage(
                            string.Empty, 
                            new Dictionary<string, string> { {Headers.EnclosedMessageTypes, typeof(MyRequest).FullName} }, 
                            stream.ToArray());
                        var transportOperation = new TransportOperation(message, new UnicastAddressTag(settings.EndpointName().ToString()));
                        await dispatchMessages.Dispatch(new TransportOperations(transportOperation), new ContextBag());
                    }
                }

                public Task Stop(IBusSession context)
                {
                    return context.Completed();
                }
            }

            class MyEventHandler : IHandleMessages<MyRequest>
            {
                private readonly MyContext myContext;

                public MyEventHandler(MyContext myContext)
                {
                    this.myContext = myContext;
                }

                public Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    myContext.GotTheMessage = true;

                    return context.Completed();
                }
            }
        }

        class MyRequest : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool GotTheMessage { get; set; }
        }
    }
}