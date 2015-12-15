namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class ReadIncomingCallbackAddressBehavior : Behavior<IncomingLogicalMessageContext>
    {
        public override async Task Invoke(IncomingLogicalMessageContext context, Func<Task> next)
        {
            string incomingCallbackQueue;
            if (context.Message != null && context.Headers.TryGetValue(RabbitMqMessageSender.CallbackHeaderKey, out incomingCallbackQueue))
            {
                context.Extensions.Set(RabbitMqMessageSender.CallbackHeaderKey, incomingCallbackQueue);
            }

            await next().ConfigureAwait(false);
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("ReadIncomingCallbackAddressBehavior", typeof(ReadIncomingCallbackAddressBehavior), "Reads the callback address specified by the message sender and puts it into the context.")
            {
            }
        }
    }
}