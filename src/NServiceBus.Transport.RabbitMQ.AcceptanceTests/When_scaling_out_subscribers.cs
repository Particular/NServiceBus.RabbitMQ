namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_scaling_out_subscribers : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_only_deliver_event_to_one_of_the_instances()
        {
            var context = await Scenario.Define<MyContext>()
                   .WithEndpoint<Publisher>(b => b.When(c => c.ServerASubscribed && c.ServerBSubscribed, bus => bus.Publish<MyEvent>()))
                   .WithEndpoint<ScaledOutSubscriber>(b =>
                   {
                       b.CustomConfig(c => c.MakeInstanceUniquelyAddressable("A"));
                       b.When(async (bus, c) =>
                       {
                           await bus.Subscribe<MyEvent>();
                           c.ServerASubscribed = true;
                       });
                   })
                   .WithEndpoint<ScaledOutSubscriber>(b =>
                   {
                       b.CustomConfig(c => c.MakeInstanceUniquelyAddressable("B"));
                       b.When(async (bus, c) =>
                       {
                           await bus.Subscribe<MyEvent>();
                           c.ServerBSubscribed = true;
                       });
                   })
                   .Done(ctx => ctx.Counter > 0)
                   .Run();

            Assert.That(context.Counter, Is.EqualTo(1), "One of the scaled out instances should get the event");
        }

        public class ScaledOutSubscriber : EndpointConfigurationBuilder
        {
            public ScaledOutSubscriber() => EndpointSetup<DefaultPublisher>();

            class MyEventHandler(MyContext myContext) : IHandleMessages<MyEvent>
            {
                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    myContext.IncrementCounter();
                    return Task.CompletedTask;
                }
            }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher() => EndpointSetup<DefaultPublisher>();
        }

        public class MyEvent : IEvent;

        class MyContext : ScenarioContext
        {
            public bool ServerASubscribed { get; set; }
            public bool ServerBSubscribed { get; set; }
            public int Counter => counter;

            public void IncrementCounter() => Interlocked.Increment(ref counter);

            int counter;
        }
    }
}