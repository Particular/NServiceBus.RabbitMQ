namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;
    using Pipeline.Contexts;

    class OpenPublishChannelBehavior : Behavior<IncomingLogicalMessageContext>
    {
        public IChannelProvider ChannelProvider { get; set; }

        public override async Task Invoke(IncomingLogicalMessageContext context, Func<Task> next)
        {
            var lazyChannel = new Lazy<ConfirmsAwareChannel>(() => ChannelProvider.GetNewPublishChannel());

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
                InsertAfter(WellKnownStep.CreateChildContainer);
                InsertBefore(WellKnownStep.ExecuteUnitOfWork);
            }
        }
    }
}