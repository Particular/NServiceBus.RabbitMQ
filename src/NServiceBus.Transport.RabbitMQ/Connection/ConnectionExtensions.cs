namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using global::RabbitMQ.Client;

    static class ConnectionExtensions
    {
        public static void VerifyBrokerRequirements(this IConnection connection)
        {
            var minimumBrokerVersion = Version.Parse("3.10.0");
            var brokerVersionString = Encoding.UTF8.GetString((byte[])connection.ServerProperties["version"]);

            if (Version.TryParse(brokerVersionString, out var brokerVersion) && brokerVersion < minimumBrokerVersion)
            {
                throw new Exception($"An unsupported broker version was detected: {brokerVersion}. The broker must be at least version {minimumBrokerVersion}.");
            }

            using var channel = connection.CreateModel();

            var arguments = new Dictionary<string, object> { { "x-queue-type", "stream" } };

            try
            {
                channel.QueueDeclare("nsb.v2.verify-stream-flag-enabled", true, false, false, arguments);
            }
            catch (Exception ex) when (ex.Message.Contains("the corresponding feature flag is disabled"))
            {
                throw new Exception("An unsupported broker configuration was detected. The 'stream_queue' feature flag needs to be enabled.");
            }
        }
    }
}
