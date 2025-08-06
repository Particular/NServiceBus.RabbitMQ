namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System.CommandLine;

    class QueueValidateDeliveryLimitCommand(string queueName, BrokerVerifier brokerVerifier, TextWriter output)
    {
        public static Command CreateCommand()
        {
            var command = new Command("validate-delivery-limit", "Validate that the queue has an unlimited delivery limit");

            var queueNameArgument = new Argument<string>("queueName")
            {
                Description = "The name of the queue to validate"
            };

            var brokerVerifierBinder = SharedOptions.CreateBrokerVerifierBinderWithOptions(command);

            command.Arguments.Add(queueNameArgument);

            command.SetAction(async (parseResult, cancellationToken) =>
            {
                var brokerVerifier = brokerVerifierBinder.CreateBrokerVerifier(parseResult);

                var validateCommand = new QueueValidateDeliveryLimitCommand(parseResult.GetRequiredValue(queueNameArgument), brokerVerifier, parseResult.Configuration.Output);
                await validateCommand.Run(cancellationToken);
            });

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
                output.WriteLine(ex.Message);
            }
        }
    }
}
