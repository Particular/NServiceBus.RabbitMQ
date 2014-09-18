namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Support;
    using NUnit.Framework;

    public class When_scaling_out_subscribers
    {
        [Test]
        public void Should_only_deliver_event_to_one_of_the_instances()
        {
            var context = new Context();

            Scenario.Define(context)
                   .WithEndpoint<Publisher>(b => b.When(c => c.ServerASubscribed && c.ServerBSubscribed, bus => bus.Publish<MyEvent>()))
                   .WithEndpoint<ScaledOutSubscriber>(b =>
                   {
                       b.CustomConfig(c => RuntimeEnvironment.MachineNameAction = () => "ScaledOutServerA");
                       b.Given((bus, c) =>
                       {
                           bus.Subscribe<MyEvent>();
                           c.ServerASubscribed = true;
                       });
                   })
                   .WithEndpoint<ScaledOutSubscriber>(b =>
                   {
                       b.CustomConfig(c => RuntimeEnvironment.MachineNameAction = () => "ScaledOutServerB");
                       b.Given((bus, c) =>
                       {
                           bus.Subscribe<MyEvent>();
                           c.ServerBSubscribed = true;
                       });
                   
                   })
                   //.Done(c => context.ServerAGotTheEvent && context.ServerBGotTheEvent)
                   .Run(new RunSettings { UseSeparateAppDomains = true, TestExecutionTimeout = TimeSpan.FromSeconds(20) });


            Assert.False(context.ServerAGotTheEvent && context.ServerBGotTheEvent, "Both scaled out instances should not get the event");
            Assert.True(context.ServerAGotTheEvent || context.ServerBGotTheEvent, "One of the scaled out instances should get the event");
        }

        public class ScaledOutSubscriber : EndpointConfigurationBuilder
        {
            public ScaledOutSubscriber()
            {
                //note the scaleout setting will make pubsub break for now
                EndpointSetup<DefaultPublisher>(c => c.ScaleOut().UseUniqueBrokerQueuePerMachine());
                //EndpointSetup<DefaultPublisher>();
            }

            class MyEventHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }
                public void Handle(MyEvent message)
                {
                    if (RuntimeEnvironment.MachineName == "ScaledOutServerA")
                    {
                        Context.ServerAGotTheEvent = true;
                    }

                    if (RuntimeEnvironment.MachineName == "ScaledOutServerB")
                    {
                        Context.ServerBGotTheEvent = true;
                    }
                }
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

        class Context : ScenarioContext
        {
            public bool ServerASubscribed { get; set; }
            public bool ServerBSubscribed { get; set; }
            public bool ServerBGotTheEvent { get; set; }
            public bool ServerAGotTheEvent { get; set; }
        }
    }
}