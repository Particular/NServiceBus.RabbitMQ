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

            await endpoint.Send(new Command1());
            await endpoint.Send(new Command2());
            await endpoint.Publish(new DerivedEvent());

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
            return context.Reply(new Reply());
        }
    }

    class BaseEvent : IEvent
    {
        
    }

    class DerivedEvent : BaseEvent
    {
    }

    class DerivedEventHandler : IHandleMessages<DerivedEvent>
    {
        public Task Handle(DerivedEvent message, IMessageHandlerContext context)
        {
            Console.WriteLine($"Got {message.GetType().FullName}");
            return Task.FromResult(0);
        }
    }

    class Reply : IMessage
    {
    }

    class ReplyHandler : IHandleMessages<Reply>
    {
        public Task Handle(Reply message, IMessageHandlerContext context)
        {
            Console.WriteLine($"Got {message.GetType().FullName}");
            return Task.FromResult(0);
        }
    }
}
