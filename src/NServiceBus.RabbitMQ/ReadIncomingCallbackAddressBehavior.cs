namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class ReadIncomingCallbackAddressBehavior : IBehavior<IncomingContext>
    {
        public void Invoke(IncomingContext context, Action next)
        {
            string incomingCallbackQueue;
            if (context.IncomingLogicalMessage != null && context.IncomingLogicalMessage.Headers.TryGetValue(RabbitMqMessageSender.CallbackHeaderKey, out incomingCallbackQueue))
            {
                context.Set(RabbitMqMessageSender.CallbackHeaderKey, incomingCallbackQueue);
            }
            next();
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("ReadIncomingCallbackAddressBehavior", typeof(ReadIncomingCallbackAddressBehavior), "Reads the callback address specified by the message sender and puts it into the context.")
            {
                InsertBefore(WellKnownStep.LoadHandlers);
            }
        }
    }
}