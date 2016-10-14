using System;
using System.Threading.Tasks;

namespace CommandWireSample
{
    using NServiceBus;

    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var cfg = new EndpointConfiguration("WireSample");
            cfg.UsePersistence<InMemoryPersistence>();
            cfg.UseTransport<RabbitMQTransport>().ConnectionString("host=localhost").UseAutomaticRoutingTopology();
            cfg.SendFailedMessagesTo("error");

            var endpoint = await Endpoint.Start(cfg);


            Console.WriteLine("Press <enter> to send message");
            Console.ReadLine();

            var options = new SendOptions();
            //options.SetDestination("Dupa");
            await endpoint.Send(new Command1(), options);

            Console.WriteLine("Press <enter> to exit.");
            Console.ReadLine();

            await endpoint.Stop();
        }
    }

    class BaseCommand : ICommand
    {
    }

    class Command1 : BaseCommand
    {
    }

    class Command1Handler : IHandleMessages<Command1>
    {
        public Task Handle(Command1 message, IMessageHandlerContext context)
        {
            Console.WriteLine($"Got {message.GetType().FullName}");
            return Task.FromResult(0);
        }
    }

    class Command2 : ICommand
    {
    }

    class Command2Handler : IHandleMessages<Command2>
    {
        public Task Handle(Command2 message, IMessageHandlerContext context)
        {
            Console.WriteLine($"Got {message.GetType().FullName}");
            return Task.FromResult(0);
        }
    }
}
