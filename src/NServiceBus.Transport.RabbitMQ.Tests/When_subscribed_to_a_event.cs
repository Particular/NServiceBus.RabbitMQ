
namespace NServiceBus.Transport.RabbitMQ.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;
    using Unicast.Messages;

    [TestFixture]
    class When_subscribed_to_a_event : RabbitMqContext
    {
        [Test]
        public async Task Should_receive_published_events_of_that_type()
        {
            await Subscribe<MyEvent>();

            await Publish<MyEvent>();

            AssertReceived<MyEvent>();
        }

        [Test]
        public async Task Should_receive_the_event_if_subscribed_to_the_base_class()
        {
            await Subscribe<EventBase>();

            await Publish<SubEvent1>();
            await Publish<SubEvent2>();

            AssertReceived<SubEvent1>();
            AssertReceived<SubEvent2>();
        }

        [Test]
        public async Task Should_not_receive_the_event_if_subscribed_to_the_specific_class()
        {
            await Subscribe<SubEvent1>();

            await Publish<EventBase>();

            AssertNoEventReceived();
        }

        [Test]
        public async Task Should_receive_the_event_if_subscribed_to_the_base_interface()
        {
            await Subscribe<IMyEvent>();

            await Publish<IMyEvent1>();
            await Publish<IMyEvent2>();

            AssertReceived<IMyEvent1>();
            AssertReceived<IMyEvent2>();
        }

        [Test]
        public async Task Should_not_receive_the_event_if_subscribed_to_specific_interface()
        {
            await Subscribe<IMyEvent1>();

            await Publish<IMyEvent>();

            AssertNoEventReceived();
        }

        [Test]
        public async Task Should_not_receive_events_of_other_types()
        {
            await Subscribe<MyEvent>();

            //publish a event that that this publisher isn't subscribed to
            await Publish<MyOtherEvent>();
            await Publish<MyEvent>();

            AssertReceived<MyEvent>();
        }

        [Test]
        public async Task Subscribing_to_IEvent_should_subscribe_to_all_published_messages()
        {
            await Subscribe<IEvent>();

            await Publish<MyOtherEvent>();
            await Publish<MyEvent>();

            AssertReceived<MyOtherEvent>();
            AssertReceived<MyEvent>();
        }

        [Test]
        public async Task Subscribing_to_Object_should_subscribe_to_all_published_messages()
        {
            await Subscribe<object>();

            await Publish<MyOtherEvent>();
            await Publish<MyEvent>();

            AssertReceived<MyOtherEvent>();
            AssertReceived<MyEvent>();
        }

        [Test]
        public async Task Subscribing_to_a_class_implementing_a_interface_should_only_give_the_concrete_class()
        {
            await Subscribe<CombinedClassAndInterface>();

            await Publish<CombinedClassAndInterface>();
            await Publish<IMyEvent>();

            AssertReceived<CombinedClassAndInterface>();
            AssertNoEventReceived();
        }

        [Test]
        public async Task Subscribing_to_a_interface_that_is_implemented_be_a_class_should_give_the_event_if_the_class_is_published()
        {
            await Subscribe<IMyEvent>();

            await Publish<CombinedClassAndInterface>();
            await Publish<IMyEvent>();

            AssertReceived<CombinedClassAndInterface>();
            AssertReceived<IMyEvent>();
        }

        [Test]
        public async Task Should_not_receive_events_after_unsubscribing()
        {
            await Subscribe<MyEvent>();

            await subscriptionManager.Unsubscribe(new MessageMetadata(typeof(MyEvent)), new ContextBag());

            //publish a event that that this publisher isn't subscribed to
            await Publish<MyEvent>();

            AssertNoEventReceived();
        }

        [SetUp]
        public async Task Prepare()
        {
            await Unsubscribe<IEvent>();
            await Unsubscribe<object>();
            await Unsubscribe<MyOtherEvent>();
            await Unsubscribe<MyEvent>();
            await Unsubscribe<EventBase>();
            await Unsubscribe<SubEvent1>();
            await Unsubscribe<SubEvent2>();
            await Unsubscribe<IMyEvent>();
            await Unsubscribe<IMyEvent1>();
            await Unsubscribe<IMyEvent2>();
            await Unsubscribe<CombinedClassAndInterface>();
        }

        Task Subscribe<T>(CancellationToken cancellationToken = default) => subscriptionManager.SubscribeAll(new[] { new MessageMetadata(typeof(T)) }, new ContextBag(), cancellationToken);
        Task Unsubscribe<T>(CancellationToken cancellationToken = default) => subscriptionManager.Unsubscribe(new MessageMetadata(typeof(T)), new ContextBag(), cancellationToken);

        Task Publish<T>(CancellationToken cancellationToken = default)
        {
            var type = typeof(T);
            var message = new OutgoingMessageBuilder().WithBody(new byte[0]).CorrelationId(type.FullName).PublishType(type).Build();

            return messageDispatcher.Dispatch(message, new TransportTransaction(), cancellationToken);
        }

        void AssertReceived<T>()
        {
            var receivedMessage = ReceiveMessage();

            AssertReceived<T>(receivedMessage);
        }

        void AssertReceived<T>(IncomingMessage receivedEvent)
        {
            Assert.That(receivedEvent.Headers[Headers.CorrelationId], Is.EqualTo(typeof(T).FullName));
        }

        void AssertNoEventReceived()
        {
            var messageWasReceived = TryWaitForMessageReceipt();

            Assert.That(messageWasReceived, Is.False);
        }
    }

    public class MyOtherEvent
    {
    }

    public class MyEvent : IMessage
    {
    }

    public class EventBase : IEvent
    {

    }

    public class SubEvent1 : EventBase
    {

    }

    public class SubEvent2 : EventBase
    {

    }

    public interface IMyEvent : IEvent
    {

    }

    public interface IMyEvent1 : IMyEvent
    {

    }

    public interface IMyEvent2 : IMyEvent
    {

    }

    public class CombinedClassAndInterface : IMyEvent
    {

    }
}
