namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
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
                       //note the scaleout setting will make pubsub break for now
                       b.CustomConfig(c => c.ScaleOut().InstanceDiscriminator("InstanceA"));
                       b.When((bus, c) =>
                       {
                           bus.Subscribe<MyEvent>();
                           c.ServerASubscribed = true;
                           return bus.Completed();
                       });
                   })
                   .WithEndpoint<ScaledOutSubscriber>(b =>
                   {
                       //note the scaleout setting will make pubsub break for now
                       b.CustomConfig(c => c.ScaleOut().InstanceDiscriminator("InstanceB"));
                       b.When((bus, c) =>
                       {
                           bus.Subscribe<MyEvent>();
                           c.ServerBSubscribed = true;
                           return bus.Completed();
                       });
                   })
                   .Run(TimeSpan.FromSeconds(10));

            Assert.AreEqual(1, context.Counter, "One of the scaled out instances should get the event");
        }

        public class ScaledOutSubscriber : EndpointConfigurationBuilder
        {
            public ScaledOutSubscriber()
            {
                EndpointSetup<DefaultPublisher>();
            }

            class MyEventHandler : IHandleMessages<MyEvent>
            {
                readonly MyContext myContext;

                public MyEventHandler(MyContext context)
                {
                    myContext = context;
                }

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    lock (objLock)
                    {
                        myContext.Counter++;
                    }

                    return context.Completed();
                }

                static Object objLock = new object();
            }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>();
            }
        }

        class MyEvent : IEvent
        {

        }

        class MyContext : ScenarioContext
        {
            public bool ServerASubscribed { get; set; }
            public bool ServerBSubscribed { get; set; }
            public int Counter { get; set; }
        }
    }
}