namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Extensibility;
    using Features;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Routing;
    using Settings;

    public class When_using_a_custom_message_id_strategy : NServiceBusAcceptanceTest
    {
        const string customMessageId = "CustomMessageId";

        [Test]
        public async Task Should_use_custom_strategy_to_set_message_id_on_message_with_no_id()
        {
            var context = await Scenario.Define<MyContext>()
                   .WithEndpoint<Receiver>()
                   .Done(c => c.GotTheMessage)
                   .Run();

            Assert.True(context.GotTheMessage, "Should receive the message");
            Assert.AreEqual(context.ReceivedMessageId, customMessageId, "Message Id should equal custom Id value");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableFeature<StarterFeature>();
                    c.UseTransport<RabbitMQTransport>()
                        .CustomMessageIdStrategy(m => customMessageId);
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

                    protected override Task OnStart(IMessageSession session)
                    {
                        //Use feature to send message that has no message id

                        var messageBody = "<MyRequest></MyRequest>";

                        var message = new OutgoingMessage(
                            string.Empty,
                            new Dictionary<string, string>
                            {
                                    { Headers.EnclosedMessageTypes, typeof(MyRequest).FullName }
                            },
                            Encoding.UTF8.GetBytes(messageBody));

                        var transportOperation = new TransportOperation(message, new UnicastAddressTag(settings.EndpointName()));
                        return dispatchMessages.Dispatch(new TransportOperations(transportOperation), new TransportTransaction(), new ContextBag());
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
                    myContext.ReceivedMessageId = context.MessageId;

                    return TaskEx.CompletedTask;
                }
            }
        }

        class MyRequest : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool GotTheMessage { get; set; }
            public string ReceivedMessageId { get; set; }
        }
    }
}