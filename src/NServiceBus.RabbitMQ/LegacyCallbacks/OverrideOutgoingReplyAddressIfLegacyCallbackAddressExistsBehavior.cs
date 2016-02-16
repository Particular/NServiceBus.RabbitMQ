namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;
    using NServiceBus.Routing;
    using NServiceBus.Transports.RabbitMQ;

    class OverrideOutgoingReplyAddressIfLegacyCallbackAddressExistsBehavior : Behavior<IRoutingContext>
    {
        public override Task Invoke(IRoutingContext context, Func<Task> next)
        {
            LegacyCallbackAddress state;
            string messageIntent;

            if (context.Extensions.TryGet(out state)
                && context.Message.Headers.TryGetValue(Headers.MessageIntent, out messageIntent)
                && messageIntent == MessageIntentEnum.Reply.ToString())
            {
                context.RoutingStrategies = new[]
                {
                    new UnicastRoutingStrategy(state.Address)
                };
            }

            return next();
        }
    }
}