namespace NServiceBus.Transport.RabbitMQ.Tests.DelayedDelivery
{
    using NUnit.Framework;

    [TestFixture]
    public class When_calculating_a_routing_key
    {
        [TestCase(0, "some-address", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.some-address", 0)]
        [TestCase(0, "", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.", 0)]
        [TestCase(0, "exotic-😊-address", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.exotic-😊-address", 0)]
        [TestCase(1, "some-address", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.some-address", 0)]
        [TestCase(2, "some-address", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.0.some-address", 1)]
        [TestCase(3, "🔥-unicode", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.1.🔥-unicode", 1)]
        [TestCase(10, "some-address", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.0.1.0.some-address", 3)]
        [TestCase(DelayInfrastructure.MaxDelayInSeconds - 1, "almost-max", "1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.0.almost-max", 27)]
        [TestCase(DelayInfrastructure.MaxDelayInSeconds, "some-address", "1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.some-address", 27)]
        [TestCase(28, "short-delay", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.1.1.0.0.short-delay", 4)]
        [TestCase(15, "small-delay", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.1.1.1.small-delay", 3)]
        public void Should_return_correct_routing_key_based_on_delay(int delayInSeconds, string address, string expectedRoutingKey, int expectedStartingDelayLevel)
        {
            var result = DelayInfrastructure.CalculateRoutingKey(delayInSeconds, address, out var startingDelayLevel);

            Assert.That(result, Is.EqualTo(expectedRoutingKey));
            Assert.That(startingDelayLevel, Is.EqualTo(expectedStartingDelayLevel));
        }

        [Test]
        public void Should_return_routing_key_with_delay_zero_seconds_for_negative_delay()
        {
            var result = DelayInfrastructure.CalculateRoutingKey(-123, "some-address", out var startingDelayLevel);

            Assert.That(result, Is.EqualTo("0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.some-address"));
            Assert.That(startingDelayLevel, Is.EqualTo(0));
        }
    }
}