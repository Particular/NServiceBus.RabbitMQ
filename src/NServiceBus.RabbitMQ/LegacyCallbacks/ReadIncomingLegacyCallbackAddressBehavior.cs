namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;
    using NServiceBus.Transports.RabbitMQ;

    class ReadIncomingLegacyCallbackAddressBehavior : Behavior<IIncomingLogicalMessageContext>
    {
        public override Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
        {
            string incomingCallbackQueue;

            if (context.Message != null && context.Headers.TryGetValue("NServiceBus.RabbitMQ.CallbackQueue", out incomingCallbackQueue))
            {
                context.Extensions.Set(new LegacyCallbackAddress(incomingCallbackQueue));
            }

            return next();
        }
    }
}