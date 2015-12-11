//namespace NServiceBus.RabbitMQ.AcceptanceTests
//{
//    using NServiceBus.AcceptanceTesting;
//    using NServiceBus.AcceptanceTests.EndpointTemplates;
//    using NUnit.Framework;

//    public class When_callback_receiver_is_disabled
//    {
//        [Test]
//        public void Should_still_receive_callbacks()
//        {
//            var context = new Context();

//            Scenario.Define(context)
//                   .WithEndpoint<ServerThatRespondsToCallbacks>()
//                   .WithEndpoint<ScaledOutClient>(b => b.Given((bus, c) => bus.Send(new MyRequest())
//                       .Register(m =>
//                       {
//                           c.GotTheCallback = true;
//                       })))
//                   .Done(c => context.GotTheCallback)
//                   .Run();

//            Assert.True(context.GotTheCallback, "Should get the callback");
//        }

//        public class ScaledOutClient : EndpointConfigurationBuilder
//        {
//            public ScaledOutClient()
//            {
//                EndpointSetup<DefaultServer>(c => c.UseTransport<RabbitMQTransport>().DisableCallbackReceiver())
//                    .AddMapping<MyRequest>(typeof(ServerThatRespondsToCallbacks));
//            }
//        }

//        public class ServerThatRespondsToCallbacks : EndpointConfigurationBuilder
//        {
//            public ServerThatRespondsToCallbacks()
//            {
//                EndpointSetup<DefaultServer>();
//            }

//            class MyEventHandler : IHandleMessages<MyRequest>
//            {
//                public IBus Bus { get; set; }

//                public void Handle(MyRequest message)
//                {
//                    Bus.Return(1);
//                }
//            }
//        }

//        class MyRequest : IMessage
//        {
//        }

//        class Context : ScenarioContext
//        {
//            public bool GotTheCallback { get; set; }
//        }
//    }
//}