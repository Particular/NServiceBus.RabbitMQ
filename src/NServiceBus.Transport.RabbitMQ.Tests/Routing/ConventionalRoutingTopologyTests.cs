namespace NServiceBus.Transport.RabbitMQ.Tests.Routing
{
    using System.Collections.Generic;
    using global::RabbitMQ.Client;
    using Moq;
    using NUnit.Framework;
    using Unicast.Messages;

    [TestFixture]
    class ConventionalRoutingTopologyTests
    {
        class TestMessage
        {
        }

        class GenericMessage<TTarget, TSource>
        {
        }

        ConventionalRoutingTopology topology;
        Mock<IModel> model;

        [SetUp]
        public void SetUp()
        {
            topology = new ConventionalRoutingTopology(useDurableEntities: true);

            model = new Mock<IModel>();
        }

        [TearDown]
        public void TearDown()
        {
            model.Verify();
            model.VerifyNoOtherCalls();
        }

        [Test]
        public void SetupTypeSubscriptions_When_GenericType()
        {
            const string expectedExchangeName = "System.Collections.Generic:List--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--";

            // exchange
            model.Setup(x => x.ExchangeDeclare(expectedExchangeName, ExchangeType.Fanout, true, false, null)).Verifiable();
            model.Setup(x => x.ExchangeDeclare("System:Object", ExchangeType.Fanout, true, false, null)).Verifiable();
            // exchange for interface
            model.Setup(x => x.ExchangeDeclare("System.Collections.Generic:IList--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--", ExchangeType.Fanout, true, false, null)).Verifiable();
            model.Setup(x => x.ExchangeDeclare("System.Collections.Generic:IReadOnlyList--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--", ExchangeType.Fanout, true, false, null)).Verifiable();
            model.Setup(x => x.ExchangeDeclare("System.Collections:IList", ExchangeType.Fanout, true, false, null)).Verifiable();
            model.Setup(x => x.ExchangeDeclare("System.Collections.Generic:ICollection--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--", ExchangeType.Fanout, true, false, null)).Verifiable();
            model.Setup(x => x.ExchangeDeclare("System.Collections.Generic:IEnumerable--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--", ExchangeType.Fanout, true, false, null)).Verifiable();
            model.Setup(x => x.ExchangeDeclare("System.Collections:IEnumerable", ExchangeType.Fanout, true, false, null)).Verifiable();
            model.Setup(x => x.ExchangeDeclare("System.Collections:ICollection", ExchangeType.Fanout, true, false, null)).Verifiable();
            model.Setup(x => x.ExchangeDeclare("System.Collections.Generic:IReadOnlyCollection--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--", ExchangeType.Fanout, true, false, null)).Verifiable();

            // binding
            model.Setup(x => x.ExchangeBind("System:Object", expectedExchangeName, string.Empty, null)).Verifiable();
            model.Setup(x => x.ExchangeBind("System.Collections.Generic:IList--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--", expectedExchangeName, string.Empty, null)).Verifiable();
            model.Setup(x => x.ExchangeBind("System.Collections.Generic:IReadOnlyList--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--", expectedExchangeName, string.Empty, null)).Verifiable();
            model.Setup(x => x.ExchangeBind("System.Collections:IList", expectedExchangeName, string.Empty, null)).Verifiable();
            model.Setup(x => x.ExchangeBind("System.Collections.Generic:ICollection--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--", expectedExchangeName, string.Empty, null)).Verifiable();
            model.Setup(x => x.ExchangeBind("System.Collections.Generic:IEnumerable--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--", expectedExchangeName, string.Empty, null)).Verifiable();
            model.Setup(x => x.ExchangeBind("System.Collections:IEnumerable", expectedExchangeName, string.Empty, null)).Verifiable();
            model.Setup(x => x.ExchangeBind("System.Collections:ICollection", expectedExchangeName, string.Empty, null)).Verifiable();
            model.Setup(x => x.ExchangeBind("System.Collections.Generic:IReadOnlyCollection--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--", expectedExchangeName, string.Empty, null)).Verifiable();
            model.Setup(x => x.ExchangeBind("test", expectedExchangeName, string.Empty, null)).Verifiable();

            topology.SetupSubscription(model.Object, new MessageMetadata(typeof(List<TestMessage>)), "test");
        }

        [Test]
        public void SetupTypeSubscriptions_When_GenericTypeWithTwoArgs()
        {
            const string expectedExchangeName = "NServiceBus.Transport.RabbitMQ.Tests.Routing:GenericMessage--NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage::NServiceBus.Transport.RabbitMQ.Tests.Routing:TestMessage--";

            // exchange
            model.Setup(x => x.ExchangeDeclare(expectedExchangeName, ExchangeType.Fanout, true, false, null)).Verifiable();
            model.Setup(x => x.ExchangeDeclare("System:Object", ExchangeType.Fanout, true, false, null)).Verifiable();

            // binding
            model.Setup(x => x.ExchangeBind("System:Object", expectedExchangeName, string.Empty, null)).Verifiable();
            model.Setup(x => x.ExchangeBind("test", expectedExchangeName, string.Empty, null)).Verifiable();

            topology.SetupSubscription(model.Object, new MessageMetadata(typeof(GenericMessage<TestMessage, TestMessage>)), "test");
        }
    }
}
