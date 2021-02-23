
namespace NServiceBus.Transport.RabbitMQ
{
    using System;

    class AmqpConnectionString
    {
        public static Action<RabbitMQTransport> Parse(string connectionString)
        {
            return transport =>
            {
                var uri = new Uri(connectionString);

                transport.Host = uri.Host;

                if (!uri.IsDefaultPort)
                {
                    transport.Port = uri.Port;
                }

                transport.UseTLS = uri.Scheme == "amqps";

                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var userPass = uri.UserInfo.Split(':');
                    if (userPass.Length > 2)
                    {
                        throw new Exception($"Invalid user information: {uri.UserInfo}. Expected user and password separated by a colon.");
                    }

                    transport.UserName = UriDecode(userPass[0]);
                    if (userPass.Length == 2)
                    {
                        transport.Password = UriDecode(userPass[1]);
                    }
                }

                if (uri.Segments.Length > 2)
                {
                    throw new Exception($"Multiple segments are not allowed in the path of an AMQP URI: {string.Join(", ", uri.Segments)}");
                }

                if (uri.Segments.Length == 2)
                {
                    transport.VHost = UriDecode(uri.Segments[1]);
                }
            };
        }

        static string UriDecode(string value)
        {
            return Uri.UnescapeDataString(value);
        }
    }
}