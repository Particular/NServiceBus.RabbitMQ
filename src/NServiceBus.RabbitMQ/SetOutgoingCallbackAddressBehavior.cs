namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.OutgoingPipeline;
    using NServiceBus.Pipeline;

    class SetOutgoingCallbackAddressBehavior : Behavior<OutgoingPhysicalMessageContext>
    {
        public string CallbackQueue { get; set; }

        public override Task Invoke(OutgoingPhysicalMessageContext context, Func<Task> next)
        {
            context.Headers[RabbitMqMessageSender.CallbackHeaderKey] = CallbackQueue;
            return next();
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("SetOutgoingCallbackAddressBehavior", typeof(SetOutgoingCallbackAddressBehavior), "Writes out callback address to in outgoing message.")
            {
            }
        }
    }
}