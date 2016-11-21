namespace NServiceBus.Transport.RabbitMQ
{
    static class SettingsKeys
    {
        public const string CustomMessageIdStrategy = "RabbitMQ.CustomMessageIdStrategy";
        public const string TimeToWaitBeforeTriggeringCircuitBreaker = "RabbitMQ.TimeToWaitBeforeTriggeringCircuitBreaker";
        public const string UsePublisherConfirms = "RabbitMQ.UsePublisherConfirms";
        public const string PrefetchMultiplier = "RabbitMQ.PrefetchMultiplier";
        public const string PrefetchCount = "RabbitMQ.PrefetchCount";
        public const string Port = "RabbitMQ.Port";
        public const string VirtualHost = "RabbitMQ.virtualHost";
        public const string UserName = "RabbitMQ.username";
        public const string Password = "RabbitMQ.password";
        public const string RequestedHeartbeat = "RabbitMQ.requestedHeartbeat";
        public const string RetryDelay = "RabbitMQ.retryDelay";
        public const string UseTls = "RabbitMQ.useTls";
        public const string CertPath = "RabbitMQ.certPath";
        public const string CertPassphrase = "RabbitMQ.certPassphrase";
        public const string Host = "RabbitMQ.host";
        public const string ClientProperties = "RabbitMQ.ClientProperties";
    }
}
