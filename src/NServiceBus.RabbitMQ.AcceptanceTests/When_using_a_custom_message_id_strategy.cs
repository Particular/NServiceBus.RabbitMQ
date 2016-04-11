namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Extensibility;
    using NServiceBus.MessageInterfaces;
    using NServiceBus.Routing;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Threading.Tasks;
    using NServiceBus.Features;

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
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableFeature<StarterFeature>();
                    c.UseSerialization<MyCustomSerializerDefinition>();
                    c.UseTransport<RabbitMQTransport>()
                        //just returning a guid here, not suitable for production use
                        .CustomMessageIdStrategy(m => Guid.NewGuid().ToString());
                });
            }

            class StarterFeature : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.Container.ConfigureComponent<Starter>(DependencyLifecycle.InstancePerCall);
                    context.RegisterStartupTask(b => b.Build<Starter>());
                }

                class Starter : FeatureStartupTask
                {
                    public Starter(IDispatchMessages dispatchMessages, ReadOnlySettings settings)
                    {
                        this.dispatchMessages = dispatchMessages;
                        this.settings = settings;
                    }

                    protected override async Task OnStart(IMessageSession session)
                    {
                        using (var stream = new MemoryStream())
                        {
                            var serializer = new MyCustomSerializer();
                            serializer.Serialize(new MyRequest(), stream);

                            var message = new OutgoingMessage(
                                string.Empty,
                                new Dictionary<string, string>
                                {
                                    { Headers.EnclosedMessageTypes, typeof(MyRequest).FullName }
                                },
                                stream.ToArray());

                            var transportOperation = new TransportOperation(message, new UnicastAddressTag(settings.EndpointName().ToString()));
                            await dispatchMessages.Dispatch(new TransportOperations(transportOperation), new ContextBag());
                        }
                    }

                    protected override Task OnStop(IMessageSession session) => TaskEx.CompletedTask;

                    readonly IDispatchMessages dispatchMessages;
                    readonly ReadOnlySettings settings;
                }
            }

            class MyEventHandler : IHandleMessages<MyRequest>
            {
                readonly MyContext myContext;

                public MyEventHandler(MyContext myContext)
                {
                    this.myContext = myContext;
                }

                public Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    myContext.GotTheMessage = true;

                    return TaskEx.CompletedTask;
                }
            }
        }

        [Serializable]
        class MyRequest : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool GotTheMessage { get; set; }
        }

        class MyCustomSerializerDefinition : SerializationDefinition
        {
            public override Func<IMessageMapper, IMessageSerializer> Configure(ReadOnlySettings settings)
            {
                return mapper => new MyCustomSerializer();
            }
        }

        class MyCustomSerializer : IMessageSerializer
        {
            public void Serialize(object message, Stream stream)
            {
                var serializer = new BinaryFormatter();
                serializer.Serialize(stream, message);
            }

            public object[] Deserialize(Stream stream, IList<Type> messageTypes = null)
            {
                var serializer = new BinaryFormatter();

                stream.Position = 0;
                var msg = serializer.Deserialize(stream);

                return new[]
                {
                    msg
                };
            }

            public string ContentType => "MyCustomSerializer";
        }
    }
}