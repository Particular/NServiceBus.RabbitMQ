﻿namespace NServiceBus.Transport.RabbitMQ.AcceptanceTests
{
    using System;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_quorum_endpoint_uses_classic_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_fail_to_start()
        {
            using (var connection = ConnectionHelper.ConnectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.DeclareClassicQueue(Conventions.EndpointNamingConvention(typeof(QuorumQueueEndpoint)));
            }

            var exception = Assert.CatchAsync<Exception>(async () => await Scenario.Define<ScenarioContext>()
                .WithEndpoint<QuorumQueueEndpoint>()
                .Done(c => c.EndpointsStarted)
                .Run());

            StringAssert.Contains("PRECONDITION_FAILED - inequivalent arg 'x-queue-type' for queue 'QuorumEndpointUsesClassicQueue.QuorumQueueEndpoint'", exception.Message);
            StringAssert.Contains("received the value 'quorum' of type 'longstr' but current is none", exception.Message);
        }

        class QuorumQueueEndpoint : EndpointConfigurationBuilder
        {
            public QuorumQueueEndpoint()
            {
                EndpointSetup<QuorumEndpoint>();
            }
        }
    }
}