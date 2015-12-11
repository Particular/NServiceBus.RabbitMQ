//namespace NServiceBus.RabbitMQ.AcceptanceTests
//{
//    using System;
//    using NServiceBus.AcceptanceTesting;
//    using NServiceBus.AcceptanceTesting.Support;
//    using NServiceBus.AcceptanceTests.EndpointTemplates;
//    using NServiceBus.Support;
//    using NUnit.Framework;

//    public class When_scaling_out_senders_that_uses_callbacks
//    {
//        const int numMessagesToSend = 5;

//        [Test]
//        public void Should_only_deliver_response_to_one_of_the_instances()
//        {
//            var context = new Context();

//            Scenario.Define(context)
//                .WithEndpoint<ServerThatRespondsToCallbacks>()
//                .WithEndpoint<ScaledOutClient>(b =>
//                {
//                    b.CustomConfig(c => RuntimeEnvironment.MachineNameAction = () => "ScaledOutClientA");
//                    b.Given((bus, c) =>
//                    {
//                        for (var i = 0; i < numMessagesToSend; i++)
//                        {
//                            bus.Send(new MyRequest
//                            {
//                                ReturnCode = 1
//                            })
//                                .Register<int>(r =>
//                                {
//                                    if (r != 1)
//                                    {
//                                        throw new Exception("Wrong server got the response");
//                                    }
//                                    c.ServerAGotTheCallback++;
//                                });
//                        }
//                    });
//                })
//                .WithEndpoint<ScaledOutClient>(b =>
//                {
//                    b.CustomConfig(c => RuntimeEnvironment.MachineNameAction = () => "ScaledOutClientB");
//                    b.Given((bus, c) =>
//                    {
//                        for (var i = 0; i < numMessagesToSend; i++)
//                        {
//                            bus.Send(new MyRequest
//                            {
//                                ReturnCode = 2
//                            })
//                                .Register<int>(r =>
//                                {
//                                    if (r != 2)
//                                    {
//                                        throw new Exception("Wrong server got the response");
//                                    }
//                                    c.ServerBGotTheCallback++;
//                                });
//                        }
//                    });
//                })
//                .Done(c => (context.ServerAGotTheCallback + context.ServerBGotTheCallback) >= numMessagesToSend*2)
//                .Run(new RunSettings
//                {
//                    UseSeparateAppDomains = true,
//                });

//            Assert.AreEqual(numMessagesToSend, context.ServerAGotTheCallback, "Both scaled out instances should get the callback");

//            Assert.AreEqual(numMessagesToSend, context.ServerBGotTheCallback, "Both scaled out instances should get the callback");
//        }

//        public class ScaledOutClient : EndpointConfigurationBuilder
//        {
//            public ScaledOutClient()
//            {
//                EndpointSetup<DefaultServer>()
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
//                    Bus.Return(message.ReturnCode);
//                }
//            }
//        }

//        class MyRequest : IMessage
//        {
//            public int ReturnCode { get; set; }
//        }

//        class Context : ScenarioContext
//        {
//            public int ServerAGotTheCallback { get; set; }
//            public int ServerBGotTheCallback { get; set; }
//        }
//    }
//}