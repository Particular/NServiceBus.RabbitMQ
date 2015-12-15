namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;

    class OpenPublishChannelBehavior : Behavior<IncomingPhysicalMessageContext>
    {
        private readonly IChannelProvider channelProvider;

        public OpenPublishChannelBehavior(IChannelProvider channelProvider)
        {
            this.channelProvider = channelProvider;
        }

        public override async Task Invoke(IncomingPhysicalMessageContext context, Func<Task> next)
        {
            var lazyChannel = new Lazy<ConfirmsAwareChannel>(() => channelProvider.GetNewPublishChannel());

            context.Extensions.Set(new RabbitMq_PublishChannel(lazyChannel));

            try
            {
                await next().ConfigureAwait(false);
            }
            finally
            {
                if (lazyChannel.IsValueCreated)
                {
                    lazyChannel.Value.Dispose();
                }

                context.Extensions.Remove<RabbitMq_PublishChannel>();
            }
        }

        public struct RabbitMq_PublishChannel
        {
            public RabbitMq_PublishChannel(Lazy<ConfirmsAwareChannel> obj)
            {
                LazyChannel = obj;
            }

            public Lazy<ConfirmsAwareChannel> LazyChannel { get; }
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("OpenPublishChannelBehavior", typeof(OpenPublishChannelBehavior), "Makes sure that the is a publish channel available on the pipeline")
            {
                InsertBefore(WellKnownStep.ExecuteUnitOfWork);
            }
        }
    }
}