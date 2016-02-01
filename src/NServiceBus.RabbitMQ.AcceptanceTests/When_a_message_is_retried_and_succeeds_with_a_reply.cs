namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NUnit.Framework;

    public class When_a_message_is_retried_and_succeeds_with_a_reply : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task The_message_send_to_error_queue_should_have_its_callback_receiver_header_intact()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<OriginatingEndpoint>(c => c.When(bus => bus.Send(new Request())))
                .WithEndpoint<ReceivingEndpoint>(b => b.DoNotFailOnErrorMessages())
                .WithEndpoint<ErrorSpyEndpoint>()
                .Done(c => c.Done)
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsTrue(context.Done);
            Assert.IsTrue(context.CallbackReceiverHeader.Contains("OriginatingEndpoint"));
        }

        class Request : IMessage { }

        class MyContext : ScenarioContext
        {
            public bool Done { get; set; }
            public string CallbackReceiverHeader { get; set; }
        }

        class OriginatingEndpoint : EndpointConfigurationBuilder
        {
            public OriginatingEndpoint()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<Request>(typeof(ReceivingEndpoint));
            }
        }

        class ErrorSpyEndpoint : EndpointConfigurationBuilder
        {
            public ErrorSpyEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class SpyHandler : IHandleMessages<IMessage>
            {
                private readonly MyContext myContext;

                public SpyHandler(MyContext myContext)
                {
                    this.myContext = myContext;
                }

                public Task Handle(IMessage message, IMessageHandlerContext context)
                {
                    myContext.CallbackReceiverHeader = context.MessageHeaders["NServiceBus.RabbitMQ.CallbackQueue"];
                    myContext.Done = true;

                    return context.Completed();
                }
            }
        }

        class ReceivingEndpoint : EndpointConfigurationBuilder
        {
            public ReceivingEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.DisableFeature<SecondLevelRetries>();
                })
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    })
                    .WithConfig<MessageForwardingInCaseOfFaultConfig>(c =>
                    {
                        c.ErrorQueue = "AMessageIsRetriedAndSucceedsWithAReply.ErrorSpyEndpoint";
                    });
            }

            public class RequestHandler : IHandleMessages<Request>
            {
                public Task Handle(Request message, IMessageHandlerContext context)
                {
                    throw new Exception("Simulated");
                }
            }
        }
    }
}