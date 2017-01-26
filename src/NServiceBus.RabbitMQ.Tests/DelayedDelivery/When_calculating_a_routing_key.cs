namespace NServiceBus.Transport.RabbitMQ.Tests.DelayedDelivery
{
    using NUnit.Framework;

    [TestFixture]
    public class When_calculating_a_routing_key
    {
        [TestCase(0, "some-address", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.some-address", 0)]
        [TestCase(10, "some-address", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.0.1.0.some-address", 3)]
        [TestCase(DelayInfrastructure.MaxDelayInSeconds, "some-address", "1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.1.some-address", 27)]
        public void Should_return_correct_routing_key_based_on_delay(int delayInSeconds, string address, string expectedRoutingKey, int expectedStartingDelayLevel)
        {
            int startingDelayLevel;
            var result = DelayInfrastructure.CalculateRoutingKey(delayInSeconds, address, out startingDelayLevel);

            Assert.That(result, Is.EqualTo(expectedRoutingKey));
            Assert.That(startingDelayLevel, Is.EqualTo(expectedStartingDelayLevel));
        }

        [Test]
        public void Should_return_routing_key_with_delay_zero_seconds_for_negative_delay()
        {
            int startingDelayLevel;
            var result = DelayInfrastructure.CalculateRoutingKey(-123, "some-address", out startingDelayLevel);

            Assert.That(result, Is.EqualTo("0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.some-address"));
            Assert.That(startingDelayLevel, Is.EqualTo(0));
        }
    }
}