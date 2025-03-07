namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;

    class DelaysVerifyCommand(BrokerVerifier brokerVerifier, IConsole console)
    {
        public static Command CreateCommand()
        {
            var command = new Command("verify", "Verify broker requirements for using the v2 delay infrastructure");

            var brokerVerifierBinder = SharedOptions.CreateBrokerVerifierBinderWithOptions(command);

            command.SetHandler(async (brokerVerifier, console, cancellationToken) =>
            {
                var delaysVerify = new DelaysVerifyCommand(brokerVerifier, console);
                await delaysVerify.Run(cancellationToken);
            },
            brokerVerifierBinder, Bind.FromServiceProvider<IConsole>(), Bind.FromServiceProvider<CancellationToken>());

            return command;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            try
            {
                await brokerVerifier.Initialize(cancellationToken);
                await brokerVerifier.VerifyRequirements(cancellationToken);

                console.WriteLine("All checks OK");
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                console.WriteLine($"Fail: {ex.Message}");
            }
        }
    }
}