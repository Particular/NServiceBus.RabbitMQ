#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System.Diagnostics.CodeAnalysis;
using System.Net;

record Response<T>(HttpStatusCode StatusCode, string Reason, T Value)
{
    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue => (int)StatusCode is >= 200 and <= 299;
}
