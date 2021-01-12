namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_receiving_a_reply_that_contains_a_legacy_callback_header : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task The_audit_message_should_go_to_the_correct_queue()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<OriginatingEndpoint>(c => c.When(bus =>
                {
                    return bus.Send(new Request());
                }))
                .WithEndpoint<ReceivingEndpoint>()
                .WithEndpoint<AuditSpyEndpoint>()
                .Done(c => c.IncorrectHandlerInvoked || c.AuditMessageReceived)
                .Run();

            Assert.IsFalse(context.IncorrectHandlerInvoked);
            Assert.IsTrue(context.AuditMessageReceived);
        }

        public class Request : IMessage
        {
        }

        public class Reply : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool IncorrectHandlerInvoked { get; set; }

            public bool AuditMessageReceived { get; set; }

        }

        class OriginatingEndpoint : EndpointConfigurationBuilder
        {
            public OriginatingEndpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.ConfigureTransport().Routing().RouteToEndpoint(typeof(Request), typeof(ReceivingEndpoint));
                    config.AuditProcessedMessagesTo<AuditSpyEndpoint>();
                });
            }

            class ReplyHandler : IHandleMessages<Reply>
            {
                public Task Handle(Reply message, IMessageHandlerContext context)
                {
                    return Task.CompletedTask;
                }
            }
        }

        class ReceivingEndpoint : EndpointConfigurationBuilder
        {
            public ReceivingEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class RequestHandler : IHandleMessages<Request>
            {
                public Task Handle(Request message, IMessageHandlerContext context)
                {
                    var options = new ReplyOptions();
                    options.SetHeader("NServiceBus.RabbitMQ.CallbackQueue", Conventions.EndpointNamingConvention(typeof(ReceivingEndpoint)));

                    return context.Reply(new Reply(), options);
                }
            }

            class ReplyHandler : IHandleMessages<Reply>
            {
                MyContext testContext;

                public ReplyHandler(MyContext testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Reply message, IMessageHandlerContext context)
                {
                    testContext.IncorrectHandlerInvoked = true;

                    return Task.CompletedTask;
                }
            }
        }

        class AuditSpyEndpoint : EndpointConfigurationBuilder
        {
            public AuditSpyEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            class ReplyHandler : IHandleMessages<Reply>
            {
                MyContext testContext;

                public ReplyHandler(MyContext testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Reply message, IMessageHandlerContext context)
                {
                    testContext.AuditMessageReceived = true;

                    return Task.CompletedTask;
                }
            }
        }
    }
}