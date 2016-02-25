namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;

    public static class IBusContextExtensions
    {
        static Task done = Task.FromResult(0);

        public static Task Completed(this IMessageProcessingContext context)
        {
            return done;
        }

        public static Task Completed(this IMessageSession context)
        {
            return done;
        }
    }
}