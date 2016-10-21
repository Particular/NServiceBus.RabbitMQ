using System;
using System.Threading.Tasks;

namespace NServiceBus.RabbitMQ.NativeDelaySample
{
    class Program
    {
        static void Main(string[] args)
        {
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            var configuration = new EndpointConfiguration("DelayedReceiver");

            configuration.UseTransport<RabbitMQTransport>().ConnectionString("host=localhost");
            configuration.UsePersistence<InMemoryPersistence>();
            configuration.SendFailedMessagesTo("error");

            var endpoint = await Endpoint.Start(configuration);

            await SendWithDelay(endpoint, TimeSpan.FromSeconds(10));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(11));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(1));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(2));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(20));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(30));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(60));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(23));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(7));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(71));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(450));
            await SendWithDelay(endpoint, TimeSpan.FromSeconds(10*60));

            Console.WriteLine("Sample message sent");
            Console.ReadLine();
        }

        static async Task SendWithDelay(IEndpointInstance endpoint, TimeSpan delay)
        {
            var options = new SendOptions();
            options.DelayDeliveryWith(delay);
            options.RouteToThisEndpoint();

            await endpoint.Send(new Message
            {
                MessageSentAt = DateTime.UtcNow,
                RequestedDelay = delay
            }, options);
        }

        class Message : IMessage
        {
            public DateTime MessageSentAt { get; set; }
            public TimeSpan RequestedDelay { get; set; }
        }


        class MessageHandler : IHandleMessages<Message>
        {
            public Task Handle(Message message, IMessageHandlerContext context)
            {
                Console.WriteLine($"Message received. Requested dealy:{message.RequestedDelay}. Actually dealy: {DateTime.UtcNow - message.MessageSentAt}.");

                return Task.FromResult(0);
            }
        }
    }
}
