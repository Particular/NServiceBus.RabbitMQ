namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;

    public static class IBusContextExtensions
    {
        private static Task done = Task.FromResult(0);

        public static Task Completed(this IMessageHandlerContext context)
        {
            return done;
        }

        public static Task Completed(this IMessageSession context)
        {
            return done;
        }
    }
}