#nullable enable

namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    static class ConnectionExtensions
    {
        public static Version GetBrokerVersion(this IConnection connection)
        {
            var versionValue = connection.ServerProperties?["version"];
            var versionBytes = Array.Empty<byte>();

            if (versionValue is not null)
            {
                versionBytes = (byte[])versionValue;
            }

            var brokerVersionString = Encoding.UTF8.GetString(versionBytes);

            if (!Version.TryParse(brokerVersionString, out var brokerVersion))
            {
                throw new FormatException($"Could not parse broker version: {brokerVersion}");
            }

            return brokerVersion;
        }

        public static async Task VerifyBrokerRequirements(this IConnection connection, CancellationToken cancellationToken = default)
        {
            var minimumBrokerVersion = Version.Parse("3.10.0");

            var brokerVersion = connection.GetBrokerVersion();
            if (brokerVersion < minimumBrokerVersion)
            {
                throw new Exception($"An unsupported broker version was detected: {brokerVersion}. The broker must be at least version {minimumBrokerVersion}.");
            }

            var streamsEnabled = await connection.TryCreateStream(cancellationToken).ConfigureAwait(false);
            if (!streamsEnabled)
            {
                throw new Exception("An unsupported broker configuration was detected. The 'stream_queue' feature flag needs to be enabled.");
            }
        }

        public static async Task<bool> TryCreateStream(this IConnection connection, CancellationToken cancellationToken = default)
        {
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            var arguments = new Dictionary<string, object?> { { "x-queue-type", "stream" } };

            try
            {
                await channel.QueueDeclareAsync("nsb.v2.verify-stream-flag-enabled", true, false, false, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken) && ex.Message.Contains("the corresponding feature flag is disabled"))
            {
                return false;
            }
        }
    }
}
