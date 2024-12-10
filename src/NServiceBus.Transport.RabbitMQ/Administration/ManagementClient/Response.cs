#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient;

using System.Diagnostics.CodeAnalysis;
using System.Net;

record Response<T>(HttpStatusCode StatusCode, string Reason, T Value)
{
    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue => StatusCode.IsSuccessStatusCode();
}
