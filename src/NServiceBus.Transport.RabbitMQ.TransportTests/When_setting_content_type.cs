namespace NServiceBus.Transport.RabbitMQ.TransportTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;
using NServiceBus.TransportTests;
using NUnit.Framework;

public class When_setting_content_type : NServiceBusTransportTest
{
    [Test]
    public async Task Should_use_provided_value()
    {
        var onMessageInvoked = CreateTaskCompletionSource<MessageContext>();
        IReadOnlyBasicProperties basicProps = null;

        await StartPump(
            (context, _) =>
            {
                var basicEventArgs = context.Extensions.Get<BasicDeliverEventArgs>();
                basicProps = basicEventArgs.BasicProperties;

                return onMessageInvoked.SetCompleted(context);
            },
            (_, __) => Task.FromResult(ErrorHandleResult.RetryRequired),
            TransportTransactionMode.ReceiveOnly);

        var dispatchProperties = new DispatchProperties();
        dispatchProperties.SetContentType("my-content");

        await SendMessage(InputQueueName, new Dictionary<string, string>
        {
            {"test-header", "original"}
        }, null, dispatchProperties);

        var messageContext = await onMessageInvoked.Task;

        Assert.That(basicProps.ContentType, Is.EqualTo("my-content"));
    }
}