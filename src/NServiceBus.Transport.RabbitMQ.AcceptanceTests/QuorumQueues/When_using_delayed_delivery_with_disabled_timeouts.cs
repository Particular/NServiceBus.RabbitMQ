namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_using_delayed_delivery_with_disabled_timeouts : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_not_allow_delayed_retries()
        {
            var exception = Assert.ThrowsAsync<Exception>(async () => await Scenario.Define<ScenarioContext>()
                .WithEndpoint<EndpointWithQuorumQueue>(b => b
                    .CustomConfig(config => config
                        .Recoverability().Delayed(d => d.NumberOfRetries(3))))
                .Done(c => c.EndpointsStarted)
                .Run());

            StringAssert.Contains("Delayed retries are not supported when the transport does not support delayed delivery.", exception.Message);
        }

        [Test]
        public void Should_not_allow_delayed_sends()
        {
            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await Scenario.Define<ScenarioContext>()
                    .WithEndpoint<EndpointWithQuorumQueue>(e => e.When(session =>
                    {
                        var sendOptions = new SendOptions();
                        sendOptions.RouteToThisEndpoint();
                        sendOptions.DelayDeliveryWith(TimeSpan.FromMinutes(1));
                        return session.Send(new TestMessage(), sendOptions);
                    }))
                    .Done(c => c.EndpointsStarted)
                    .Run();
            });

            StringAssert.Contains("Cannot delay delivery of messages when there is no infrastructure support for delayed messages", exception.Message);
        }

        [Test]
        public async Task Should_not_allow_saga_timeouts()
        {
            var context = await Scenario.Define<SagaTimeoutContext>()
                .WithEndpoint<EndpointWithQuorumQueue>(e => e.When(session => session.SendLocal(new StartSagaMessage())))
                .Done(c => c.SagaTimeoutException != null)
                .Run();

            Assert.NotNull(context.SagaTimeoutException);
            StringAssert.Contains("Cannot delay delivery of messages when there is no infrastructure support for delayed messages", context.SagaTimeoutException.Message);
        }

        public class SagaTimeoutContext : ScenarioContext
        {
            public Exception SagaTimeoutException { get; set; }
        }

        public class EndpointWithQuorumQueue : EndpointConfigurationBuilder
        {
            public EndpointWithQuorumQueue()
            {
                var defaultServer = new ClusterEndpoint(QueueMode.Quorum, DelayedDeliverySupport.Disabled);
                EndpointSetup(
                    defaultServer,
                    (configuration, r) =>
                    {
                        // need to configure a different error queue that isn't a classic queue.
                        configuration.SendFailedMessagesTo("error-quorum");
                    },
                    _ => { });
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
                    try
                    {
                        await RequestTimeout<SagaTimeout>(context, TimeSpan.FromMinutes(1));
                    }
                    catch (Exception ex) when (!ex.IsCausedBy(context.CancellationToken))
                    {
                        testContext.SagaTimeoutException = ex;
                    }
                }

                public Task Timeout(SagaTimeout message, IMessageHandlerContext context)
                {
                    // should not be called.
                    return Task.CompletedTask;
                }

                public class SagaData : ContainSagaData
                {
                    public Guid CorrelationProperty { get; set; }
                }
            }
        }

        class TestMessage : IMessage
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