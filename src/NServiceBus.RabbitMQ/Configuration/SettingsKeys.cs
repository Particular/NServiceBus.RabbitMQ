namespace NServiceBus.Transport.RabbitMQ
{
    static class SettingsKeys
    {
        public const string CustomMessageIdStrategy = "RabbitMQ.CustomMessageIdStrategy";
        public const string TimeToWaitBeforeTriggeringCircuitBreaker = "RabbitMQ.TimeToWaitBeforeTriggeringCircuitBreaker";
        public const string UsePublisherConfirms = "RabbitMQ.UsePublisherConfirms";
        public const string PrefetchMultiplier = "RabbitMQ.PrefetchMultiplier";
        public const string PrefetchCount = "RabbitMQ.PrefetchCount";
    }
}
