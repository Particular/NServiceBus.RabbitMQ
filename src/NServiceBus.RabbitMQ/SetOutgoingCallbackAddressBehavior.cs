namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.OutgoingPipeline;
    using NServiceBus.Pipeline;

    class SetOutgoingCallbackAddressBehavior : Behavior<IOutgoingPhysicalMessageContext>
    {
        public override Task Invoke(IOutgoingPhysicalMessageContext context, Func<Task> next)
        {
            Callbacks callbacks;
            if (context.Extensions.TryGet(out callbacks) && callbacks.Enabled)
            {
                context.Headers[Callbacks.HeaderKey] = callbacks.QueueAddress;
            }

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