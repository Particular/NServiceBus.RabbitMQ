namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using Pipeline;
    using Pipeline.Contexts;

    class OpenPublishChannelBehavior : IBehavior<IncomingContext>
    {
        public IChannelProvider ChannelProvider { get; set; }

        public void Invoke(IncomingContext context, Action next)
        {
            var lazyChannel = new Lazy<ConfirmsAwareChannel>(() => ChannelProvider.GetNewPublishChannel());

            context.Set("RabbitMq.PublishChannel", lazyChannel);

            try
            {
                next();
            }
            finally
            {
                if (lazyChannel.IsValueCreated)
                {
                    lazyChannel.Value.Dispose();
                }

                context.Remove("RabbitMq.PublishChannel");
            }
        }



        public class Registration : RegisterStep
        {
            public Registration()
                : base("OpenPublishChannelBehavior", typeof(OpenPublishChannelBehavior), "Makes sure that the is a publish channel available on the pipeline")
            {
                InsertAfter(WellKnownStep.CreateChildContainer);
                InsertBefore(WellKnownStep.ExecuteUnitOfWork);
            }
        }
    }
}