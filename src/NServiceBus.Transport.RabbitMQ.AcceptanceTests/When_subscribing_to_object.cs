namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    class When_subscribing_to_object : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_invoke_handler()
        {
            var ctx = await Scenario.Define<SubscribeToObjectContext>()
                .WithEndpoint<SubscriberEndpoint>(b
                    => b.When(async (session, c) =>
                        {
                            await session.Subscribe<object>();

                            if (c.HasNativePubSubSupport)
                            {
                                c.EndpointSubscribed = true;
                            }
                        })
                )
                .WithEndpoint<PublisherEndpoint>(b => b.When(c => c.EndpointSubscribed, session =>
                {
                    return session.SendLocal(new StartMsg());
                }))
                .Done(x => x.CatchAllHandlerWasCalled)
                .Run();

            Assert.IsTrue(ctx.EndpointSubscribed);
            Assert.IsTrue(ctx.EventPublished);
            Assert.IsTrue(ctx.CatchAllHandlerWasCalled);
        }

        class SubscribeToObjectContext : ScenarioContext
        {
            public bool EndpointSubscribed { get; set; }
            public bool EventPublished { get; set; }
            public bool CatchAllHandlerWasCalled { get; set; }
        }

        class PublisherEndpoint : EndpointConfigurationBuilder
        {
            public PublisherEndpoint()
            {
                EndpointSetup<DefaultServer>(
                config =>
                {
                    config.Conventions()
                        .DefiningCommandsAs(t => typeof(ICommand).IsAssignableFrom(t))
                        .DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || t == typeof(object))
                        .DefiningMessagesAs(t => typeof(IMessage).IsAssignableFrom(t));

                    config.OnEndpointSubscribed<SubscribeToObjectContext>((sub, context) =>
                    {
                        context.EndpointSubscribed = true;
                    });
                });
            }

            public class PublisherHandler : IHandleMessages<StartMsg>
            {
                SubscribeToObjectContext scenarioContext;

                public PublisherHandler(SubscribeToObjectContext scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public async Task Handle(StartMsg message, IMessageHandlerContext context)
                {
                    await context.Publish(new SomeEvent());
                    scenarioContext.EventPublished = true;
                }
            }
        }

        class SubscriberEndpoint : EndpointConfigurationBuilder
        {
            public SubscriberEndpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.OnEndpointSubscribed<SubscribeToObjectContext>((sub, ctx) =>
                    {
                        ctx.EndpointSubscribed = true;
                    });

                    config.Conventions()
                        .DefiningCommandsAs(t => typeof(ICommand).IsAssignableFrom(t))
                        .DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || t == typeof(object))
                        .DefiningMessagesAs(t => typeof(IMessage).IsAssignableFrom(t));
                },
                metadata =>
                {
                    metadata.RegisterPublisherFor<object>(typeof(PublisherEndpoint));
                });
            }

            public class CatchAllMessageHandler : IHandleMessages<object>
            {
                SubscribeToObjectContext scenarioContext;

                public CatchAllMessageHandler(SubscribeToObjectContext scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(object message, IMessageHandlerContext context)
                {
                    scenarioContext.CatchAllHandlerWasCalled = true;
                    return Task.FromResult(0);
                }
            }
        }

        class StartMsg : ICommand { }
        class SomeEvent : IEvent { }
    }
}