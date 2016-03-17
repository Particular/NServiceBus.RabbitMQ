namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Serialization;
    using NServiceBus.Transports.RabbitMQ;
    using NServiceBus.Unicast;
    using NUnit.Framework;

    public class When_using_a_custom_message_id_strategy
    {
        [Test]
        public void Should_be_able_to_receive_messages_with_no_id()
        {
            var context = new Context();

            Scenario.Define(context)
                   .WithEndpoint<Receiver>(b => b.Given((bus, c) =>
                   {
                       var unicastBus = (UnicastBus)bus;
                       var connectionManager = unicastBus.Builder.Build<IManageRabbitMqConnections>();

                       var serializer = unicastBus.Builder.Build<IMessageSerializer>();

                       using (var stream = new MemoryStream())
                       {
                           serializer.Serialize(new MyRequest(), stream);

                           using (var channel = connectionManager.GetPublishConnection().CreateModel())
                           {
                               var properties = channel.CreateBasicProperties();

                               //for now until we can patch the serializer to infer the type based on the root node
                               properties.Headers = new Dictionary<string, object> { { Headers.EnclosedMessageTypes, typeof(MyRequest).FullName } };

                               channel.BasicPublish(string.Empty, unicastBus.Configure.LocalAddress.Queue, true, properties, stream.ToArray());
                           }

                       }

                   }))
                   .Done(c => context.GotTheMessage)
                   .Run();

            Assert.True(context.GotTheMessage, "Should receive the message");
        }


        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c=> c.UseTransport<RabbitMQTransport>()
                    //just returning a guid here, not suitable for production use
                    .CustomMessageIdStrategy(m => Guid.NewGuid().ToString()));
            }

            class MyEventHandler : IHandleMessages<MyRequest>
            {
                public Context Context { get; set; }

                public void Handle(MyRequest message)
                {
                    Context.GotTheMessage = true;
                }
            }
        }

        class MyRequest : IMessage
        {
        }

        class Context : ScenarioContext
        {
            public bool GotTheMessage { get; set; }
        }
    }
}