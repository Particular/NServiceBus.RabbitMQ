namespace NServiceBus.Transport.RabbitMQ.CommandLine
{
    using System;
    using System.CommandLine;

    class VerifySafeDelaysCommand
    {
        public static Command CreateCommand()
        {
            var verifyCommand = new Command("verify-safe-delays", "Verifies that the broker configuration allows for safe message delays.");

            verifyCommand.SetHandler(async (CancellationToken cancellationToken) =>
            {
                var verifyProcess = new VerifySafeDelaysCommand();
                await verifyProcess.Execute(cancellationToken).ConfigureAwait(false);

            });

            return verifyCommand;
        }

        public Task Execute(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("yay");

            return Task.CompletedTask;
        }
    }
}