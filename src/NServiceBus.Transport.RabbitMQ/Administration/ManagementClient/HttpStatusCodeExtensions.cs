#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient;

using System.Net;

static class HttpStatusCodeExtensions
{
    public static bool IsSuccessStatusCode(this HttpStatusCode statusCode) => (int)statusCode is >= 200 and <= 299;
}
