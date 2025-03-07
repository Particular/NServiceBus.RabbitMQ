namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    class QueueValidateDeliveryLimitCommand(string queueName, BrokerVerifier brokerVerifier, IConsole console)
    {
        public static Command CreateCommand()
        {
            var command = new Command("validate-delivery-limit", "Validate that the queue has an unlimited delivery limit");

            var queueNameArgument = new Argument<string>()
            {
                Name = "queueName",
                Description = "The name of the queue to validate"
            };

            var brokerVerifierBinder = SharedOptions.CreateBrokerVerifierBinderWithOptions(command);

            command.AddArgument(queueNameArgument);

            command.SetHandler(async (queueName, brokerVerifier, console, cancellationToken) =>
            {
                var validateCommand = new QueueValidateDeliveryLimitCommand(queueName, brokerVerifier, console);
                await validateCommand.Run(cancellationToken);
            },
            queueNameArgument, brokerVerifierBinder, Bind.FromServiceProvider<IConsole>(), Bind.FromServiceProvider<CancellationToken>());

            return command;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            try
            {
                await brokerVerifier.Initialize(cancellationToken);
                await brokerVerifier.ValidateDeliveryLimit(queueName, cancellationToken);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                console.WriteLine(ex.Message);
            }
        }
    }
}
