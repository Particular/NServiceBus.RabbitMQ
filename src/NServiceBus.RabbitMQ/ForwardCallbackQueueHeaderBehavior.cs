namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class ForwardCallbackQueueHeaderBehavior : IBehavior<OutgoingContext>
    {
        public void Invoke(OutgoingContext context, Action next)
        {
            string callbackQueue;

            if (context.IncomingMessage != null && context.IncomingMessage.Headers.TryGetValue(RabbitMqMessageSender.CallbackHeaderKey, out callbackQueue))
            {
                context.OutgoingMessage.Headers[RabbitMqMessageSender.CallbackHeaderKey] = callbackQueue;
            }

            next();
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("ForwardCallbackQueueHeaderBehavior", typeof(ForwardCallbackQueueHeaderBehavior), "Forwards the NServiceBus.RabbitMQ.CallbackQueue header to outgoing messages")
            {
                InsertAfter(WellKnownStep.SerializeMessage);
                InsertBefore(WellKnownStep.DispatchMessageToTransport);
            }
        }
    }
}