namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class ReadIncomingCallbackAddressBehavior : Behavior<IIncomingLogicalMessageContext>
    {
        public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
        {
            string incomingCallbackQueue;
            if (context.Message != null && context.Headers.TryGetValue(Callbacks.HeaderKey, out incomingCallbackQueue))
            {
                context.Extensions.Set(new CallbackAddress(incomingCallbackQueue));
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