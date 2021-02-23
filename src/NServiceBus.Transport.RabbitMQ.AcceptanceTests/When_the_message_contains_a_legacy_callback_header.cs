namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_the_message_contains_a_legacy_callback_header : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_reply_to_an_address_sent_in_that_header()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<OriginatingEndpoint>(c => c.When(bus =>
                {
                    var options = new SendOptions();
                    options.SetHeader("NServiceBus.RabbitMQ.CallbackQueue", Conventions.EndpointNamingConvention(typeof(SpyEndpoint)));
                    return bus.Send(new Request(), options);
                }))
                .WithEndpoint<ReceivingEndpoint>(b => b.DoNotFailOnErrorMessages())
                .WithEndpoint<SpyEndpoint>()
                .Done(c => c.Done)
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsFalse(context.RepliedToWrongQueue);
        }

        public class Request : IMessage
        {
        }

        public class Reply : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool Done { get; set; }
            public bool RepliedToWrongQueue { get; set; }
        }

        class OriginatingEndpoint : EndpointConfigurationBuilder
        {
            public OriginatingEndpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                    config.ConfigureRouting().RouteToEndpoint(typeof(Request), typeof(ReceivingEndpoint)));
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
                    testContext.RepliedToWrongQueue = true;
                    testContext.Done = true;
                    return Task.CompletedTask;
                }
            }
        }

        class SpyEndpoint : EndpointConfigurationBuilder
        {
            public SpyEndpoint()
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
                    testContext.Done = true;
                    return Task.CompletedTask;
                }
            }
        }

        class ReceivingEndpoint : EndpointConfigurationBuilder
        {
            public ReceivingEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                });
            }

            public class RequestHandler : IHandleMessages<Request>
            {
                public Task Handle(Request message, IMessageHandlerContext context)
                {
                    return context.Reply(new Reply());
                }
            }
        }
    }
}