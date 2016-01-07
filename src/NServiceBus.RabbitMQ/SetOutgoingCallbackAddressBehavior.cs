namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.OutgoingPipeline;
    using NServiceBus.Pipeline;

    class SetOutgoingCallbackAddressBehavior : Behavior<IOutgoingPhysicalMessageContext>
    {
        readonly Callbacks callbacks;

        public SetOutgoingCallbackAddressBehavior(Callbacks callbacks)
        {
            this.callbacks = callbacks;
        }

        public override Task Invoke(IOutgoingPhysicalMessageContext context, Func<Task> next)
        {
            if (callbacks.Enabled)
            {
                context.Headers[Callbacks.HeaderKey] = callbacks.QueueAddress;
            }

            return next();
        }
    }
}