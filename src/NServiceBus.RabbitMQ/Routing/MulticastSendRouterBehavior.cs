namespace NServiceBus
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Pipeline;
    using Routing;

    class MulticastSendRouterConnector : StageConnector<IOutgoingSendContext, IOutgoingLogicalMessageContext>
    {
        const string optionsTypeFullName = "NServiceBus.UnicastSendRouterConnector+State";

        static Assembly coreAssembly = typeof(IEndpointInstance).Assembly;
        static Type StateType = coreAssembly.GetType(optionsTypeFullName, true);
        static PropertyInfo OptionProp = StateType.GetProperty("Option", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        static PropertyInfo ExplicitDestinationProp = StateType.GetProperty("ExplicitDestination", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        object[] emptyArgs = new object[0];

        string localAddress;

        public MulticastSendRouterConnector(string localAddress)
        {
            this.localAddress = localAddress;
        }

        public override Task Invoke(IOutgoingSendContext context, Func<IOutgoingLogicalMessageContext, Task> stage)
        {
            var destinationString = $"#type:{context.Message.MessageType.FullName}";

            object opts;
            var hasOptions = context.Extensions.TryGet(optionsTypeFullName, out opts);
            if (hasOptions)
            {
                var option = OptionProp.GetValue(opts, emptyArgs).ToString();
                if (option == "ExplicitDestination")
                {
                    destinationString = (string)ExplicitDestinationProp.GetValue(opts, emptyArgs);
                }
                else if (option == "RouteToAnyInstanceOfThisEndpoint")
                {
                    destinationString = localAddress;
                }
                else
                {
                    throw new Exception("Unsupported option: " + option);
                }
            }

            context.Headers[Headers.MessageIntent] = MessageIntentEnum.Send.ToString();

            var logicalMessageContext = this.CreateOutgoingLogicalMessageContext(
                context.Message,
                new[]
                {
                    new UnicastRoutingStrategy(destinationString)
                    //new MulticastRoutingStrategy(context.Message.MessageType) //Hack that allow core TM to work
                },
                context);

            return stage(logicalMessageContext);
        }
    }
}