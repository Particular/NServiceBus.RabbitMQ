namespace NServiceBus.Transport.RabbitMQ
{
    static class SettingsKeys
    {
        public const string CustomMessageIdStrategy = "RabbitMQ.CustomMessageIdStrategy";
        public const string TimeToWaitBeforeTriggeringCircuitBreaker = "RabbitMQ.TimeToWaitBeforeTriggeringCircuitBreaker";
        public const string UsePublisherConfirms = "RabbitMQ.UsePublisherConfirms";
        public const string PrefetchMultiplier = "RabbitMQ.PrefetchMultiplier";
        public const string PrefetchCount = "RabbitMQ.PrefetchCount";
        public const string ClientCertificates = "RabbitMQ.ClientCertificates";
        public const string DisableRemoteCertificateValidation = "RabbitMQ.DisableRemoteCertificateValidation";
        public const string UseExternalAuthMechanism = "RabbitMQ.UseExternalAuthMechanism";
        public const string EnableTimeoutManager = "RabbitMQ.EnableTimeoutManager";
        public const string UseDurableExchangesAndQueues = "RabbitMQ.UseDurableExchangesAndQueues";
    }
}
