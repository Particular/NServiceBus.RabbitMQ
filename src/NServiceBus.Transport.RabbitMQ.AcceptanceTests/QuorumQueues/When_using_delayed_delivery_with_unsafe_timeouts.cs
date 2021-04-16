namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_using_delayed_delivery_with_unsafe_timeouts : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_allow_delayed_retries()
        {
            var context = await Scenario.Define<ScenarioContext>()
                .WithEndpoint<EndpointWithUnsafeQuorumQueue>(b => b
                    .CustomConfig(config => config
                        .Recoverability().Delayed(d => d.NumberOfRetries(3))))
                .Done(c => c.EndpointsStarted)
                .Run();

            Assert.IsTrue(context.EndpointsStarted);
        }

        [Test]
        public async Task Should_allow_delayed_sends()
        {
            var context = await Scenario.Define<DelayedMessageContext>()
                .WithEndpoint<EndpointWithUnsafeQuorumQueue>(e => e.When(session =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();
                    sendOptions.DelayDeliveryWith(TimeSpan.FromSeconds(1));
                    return session.Send(new TestMessage(), sendOptions);
                }))
                .Done(c => c.ReceivedDelayedMessage)
                .Run();

            Assert.IsTrue(context.ReceivedDelayedMessage);
        }

        [Test]
        public async Task Should_allow_saga_timeouts()
        {
            var context = await Scenario.Define<SagaTimeoutContext>()
                .WithEndpoint<EndpointWithUnsafeQuorumQueue>(e => e.When(session => session.SendLocal(new StartSagaMessage())))
                .Done(c => c.SagaTimeoutReceived)
                .Run();

            Assert.IsTrue(context.SagaTimeoutReceived);
        }

        public class DelayedMessageContext : ScenarioContext
        {
            public bool ReceivedDelayedMessage { get; set; }
        }

        public class SagaTimeoutContext : ScenarioContext
        {
            public bool SagaTimeoutReceived { get; set; }
        }

        public class EndpointWithUnsafeQuorumQueue : EndpointConfigurationBuilder
        {
            public EndpointWithUnsafeQuorumQueue()
            {
                var defaultServer = new ClusterEndpoint(QueueMode.QuorumWithUnsafeTimeouts);
                EndpointSetup(
                    defaultServer,
                    (configuration, r) =>
                    {
                        // need to configure a different error queue that isn't a classic queue.
                        configuration.SendFailedMessagesTo("error-quorum");
                    },
                    _ => { });
            }

            public class TestMessageHandler : IHandleMessages<TestMessage>
            {
                DelayedMessageContext testContext;

                public TestMessageHandler(DelayedMessageContext testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(TestMessage message, IMessageHandlerContext context)
                {
                    testContext.ReceivedDelayedMessage = true;
                    return Task.CompletedTask;
                }
            }

            public class SagaWithTimeout : Saga<SagaWithTimeout.SagaData>,
                IAmStartedByMessages<StartSagaMessage>,
                IHandleTimeouts<SagaTimeout>
            {
                SagaTimeoutContext testContext;

                public SagaWithTimeout(SagaTimeoutContext testContext)
                {
                    this.testContext = testContext;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper) =>
                    mapper.MapSaga(s => s.CorrelationProperty)
                        .ToMessage<StartSagaMessage>(m => m.CorrelationProperty);

                public async Task Handle(StartSagaMessage message, IMessageHandlerContext context)
                {
                    await RequestTimeout<SagaTimeout>(context, TimeSpan.FromSeconds(1));
                }

                public Task Timeout(SagaTimeout message, IMessageHandlerContext context)
                {
                    testContext.SagaTimeoutReceived = true;
                    return Task.CompletedTask;
                }

                public class SagaData : ContainSagaData
                {
                    public Guid CorrelationProperty { get; set; }
                }
            }
        }

        public class TestMessage : IMessage
        {
        }

        public class StartSagaMessage : IMessage
        {
            public Guid CorrelationProperty { get; set; }
        }

        public class SagaTimeout : IMessage
        {
        }
    }
}