namespace NServiceBus.Transport.RabbitMQ.CommandLine.Tests.MigrateDelayedMessages
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class When_migrating_a_delayed_message
    {
        [Test]
        [TestCase(10, new int[] { 2022, 5, 6, 12, 0, 0 }, "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.0.1.0.destination", new int[] { 2022, 5, 6, 12, 0, 10 }, "destination", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.destination", 0)]
        [TestCase(32, new int[] { 2022, 5, 6, 12, 0, 0 }, "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.0.0.0.0.0.destination", new int[] { 2022, 5, 6, 12, 0, 30 }, "destination", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.0.destination", 1)]
        [TestCase(64, new int[] { 2022, 5, 6, 12, 0, 0 }, "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.0.0.0.0.0.0.destination", new int[] { 2022, 5, 6, 12, 0, 30 }, "destination", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.0.0.0.1.0.destination", 5)]
        [TestCase(128, new int[] { 2022, 5, 6, 12, 0, 0 }, "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.0.0.0.0.0.0.0.destination", new int[] { 2022, 5, 6, 12, 0, 30 }, "destination", "0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.1.1.0.0.0.1.0.destination", 6)]
        [TestCase(86400, new int[] { 2022, 5, 6, 12, 0, 0 }, "0.0.0.0.0.0.0.0.0.0.0.1.0.1.0.1.0.0.0.1.1.0.0.0.0.0.0.0.destination", new int[] { 2022, 5, 7, 0, 0, 0 }, "destination", "0.0.0.0.0.0.0.0.0.0.0.0.1.0.1.0.1.0.0.0.1.1.0.0.0.0.0.0.destination", 15)]
        [TestCase(604800, new int[] { 2022, 5, 6, 12, 0, 0 }, "0.0.0.0.0.0.0.0.1.0.0.1.0.0.1.1.1.0.1.0.1.0.0.0.0.0.0.0.destination", new int[] { 2022, 5, 9, 12, 0, 0 }, "destination", "0.0.0.0.0.0.0.0.0.1.0.1.0.1.0.0.0.1.1.0.0.0.0.0.0.0.0.0.destination", 18)]
        public void Should_generate_the_correct_routing_key(int originalDelayInSeconds, int[] originalTimeSentValues, string originalRoutingKey, int[] utcNowValues, string expectedDestination, string expectedRoutingKey, int expectedDelayLevel)
        {
            var originalTimeSent = GetDateTimeOffsetFromValues(originalTimeSentValues);
            var utcNow = GetDateTimeOffsetFromValues(utcNowValues);

            var migrateCommand = new DelaysMigrateCommand();

            (string destinationQueue, string newRoutingKey, int newDelayLevel) = migrateCommand.GetNewRoutingKey(originalDelayInSeconds, originalTimeSent, originalRoutingKey, utcNow);

            Assert.AreEqual(expectedDestination, destinationQueue);
            Assert.AreEqual(expectedRoutingKey, newRoutingKey);
            Assert.AreEqual(expectedDelayLevel, newDelayLevel);
        }

        DateTimeOffset GetDateTimeOffsetFromValues(int[] values)
        {
            return new DateTimeOffset(values[0], values[1], values[2], values[3], values[4], values[5], TimeSpan.Zero);
        }
    }
}
