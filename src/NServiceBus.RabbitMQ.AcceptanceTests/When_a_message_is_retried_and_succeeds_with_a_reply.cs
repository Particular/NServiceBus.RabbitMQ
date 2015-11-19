namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NUnit.Framework;

    [TestFixture]
    public class When_a_message_is_retried_and_succeeds_with_a_reply
    {
        [Test]
        public void The_message_send_to_error_queue_should_have_its_callback_receiver_header_intact()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<OriginatingEndpoint>(c => c.Given(bus => bus.Send(new Request())))
                .WithEndpoint<ReceivingEndpoint>()
                .WithEndpoint<ErrorSpyEndpoint>()
                .Done(c => c.Done)
                .AllowExceptions()
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsTrue(context.Done);
            Assert.IsTrue(context.CallbackReceiverHeader.Contains("OriginatingEndpoint"));
        }

        class Request : IMessage { }

        class Context : ScenarioContext
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
                public Context Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(IMessage message)
                {
                    Context.Done = true;
                    Context.CallbackReceiverHeader = Bus.CurrentMessageContext.Headers["NServiceBus.RabbitMQ.CallbackQueue"];
                }
            }
        }

        class ReceivingEndpoint : EndpointConfigurationBuilder
        {
            public ReceivingEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.DisableFeature<SecondLevelRetries>())
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
                public void Handle(Request message)
                {
                    throw new Exception("Simulated");
                }
            }
        }
    }
}