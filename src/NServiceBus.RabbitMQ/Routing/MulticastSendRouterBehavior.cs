namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;
    using Routing;

    class MulticastSendRouterConnector : StageConnector<IOutgoingSendContext, IOutgoingLogicalMessageContext>
    {
        public override Task Invoke(IOutgoingSendContext context, Func<IOutgoingLogicalMessageContext, Task> stage)
        {
            context.Headers[Headers.MessageIntent] = MessageIntentEnum.Send.ToString();

            var logicalMessageContext = this.CreateOutgoingLogicalMessageContext(
                context.Message,
                new[]
                {
                    new UnicastRoutingStrategy($"#type:{context.Message.MessageType.FullName}")
                    //new MulticastRoutingStrategy(context.Message.MessageType) //Hack that allow core TM to work
                },
                context);

            return stage(logicalMessageContext);
        }
    }
}