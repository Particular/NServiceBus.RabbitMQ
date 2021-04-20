namespace NServiceBus.Transport.RabbitMQ.Tests.DelayedDelivery
{
    using NUnit.Framework;

    [TestFixture]
    public class When_building_delayinfrastructure
    {
        [TestCase(0, null, "nsb.delay-level-00")]
        [TestCase(10, null, "nsb.delay-level-10")]
        [TestCase(1, null, "nsb.delay-level-01")]
        [TestCase(27, null, "nsb.delay-level-27")]
        [TestCase(0, "test", "test.nsb.delay-level-00")]
        [TestCase(3, "test", "test.nsb.delay-level-03")]
        [TestCase(15, "test", "test.nsb.delay-level-15")]
        [TestCase(21, "test", "test.nsb.delay-level-21")]
        public void Should_calculate_LevelName_correctly(int level, string prefix, string expectedQueueName)
        {
            var result = DelayInfrastructure.LevelName(level, prefix);

            Assert.That(result, Is.EqualTo(expectedQueueName));
        }
    }
}