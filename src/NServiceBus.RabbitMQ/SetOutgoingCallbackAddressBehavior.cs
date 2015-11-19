namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class SetOutgoingCallbackAddressBehavior : IBehavior<OutgoingContext>
    {
        public string CallbackQueue { get; set; }

        public void Invoke(OutgoingContext context, Action next)
        {
            context.OutgoingMessage.Headers[RabbitMqMessageSender.CallbackHeaderKey] = CallbackQueue;
            next();
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("SetOutgoingCallbackAddressBehavior", typeof(SetOutgoingCallbackAddressBehavior), "Writes out callback address to in outgoing message.")
            {
                InsertAfter(WellKnownStep.SerializeMessage);
                InsertBefore(WellKnownStep.DispatchMessageToTransport);
            }
        }
    }
}