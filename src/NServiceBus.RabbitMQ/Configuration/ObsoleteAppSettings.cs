namespace NServiceBus.Transport.RabbitMQ
{
    using System.Configuration;
    using Logging;

    static class ObsoleteAppSettings
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ObsoleteAppSettings));

        public static StartupCheckResult Check()
        {
            var timeToWaitBeforeTriggering = ConfigurationManager.AppSettings["NServiceBus/RabbitMqDequeueStrategy/TimeToWaitBeforeTriggering"];

            if (timeToWaitBeforeTriggering != null)
            {
                var message = "The 'TimeToWaitBeforeTriggering' configuration setting has been removed. Use 'EndpointConfiguration.TimeToWaitBeforeTriggeringCircuitBreaker' instead.";

                Logger.Error(message);

                return StartupCheckResult.Failed(message);
            }

            var delayAfterFailure = ConfigurationManager.AppSettings["NServiceBus/RabbitMqDequeueStrategy/DelayAfterFailure"];

            if (delayAfterFailure != null)
            {
                var message = "The 'DelayAfterFailure' configuration setting has been removed. Consult the documentation for further information.";

                Logger.Error(message);

                return StartupCheckResult.Failed(message);
            }

            return StartupCheckResult.Success;
        }
    }
}
