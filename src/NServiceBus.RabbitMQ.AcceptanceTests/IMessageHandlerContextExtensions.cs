namespace NServiceBus.RabbitMQ.AcceptanceTests
{
    using System.Threading.Tasks;

    public static class IBusContextExtensions
    {
        private static Task done = Task.FromResult(0);

        public static Task Completed(this IBusContext context)
        {
            return done;
        } 
    }
}