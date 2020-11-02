namespace NServiceBus.Transport.RabbitMQ
{
    static class SettingsKeys
    {
        public const string CustomMessageIdStrategy = "RabbitMQ.CustomMessageIdStrategy";
        public const string TimeToWaitBeforeTriggeringCircuitBreaker = "RabbitMQ.TimeToWaitBeforeTriggeringCircuitBreaker";
        public const string PrefetchMultiplier = "RabbitMQ.PrefetchMultiplier";
        public const string PrefetchCount = "RabbitMQ.PrefetchCount";
        public const string ClientCertificateCollection = "RabbitMQ.ClientCertificateCollection";
        public const string DisableRemoteCertificateValidation = "RabbitMQ.DisableRemoteCertificateValidation";
        public const string UseExternalAuthMechanism = "RabbitMQ.UseExternalAuthMechanism";
        public const string UseDurableExchangesAndQueues = "RabbitMQ.UseDurableExchangesAndQueues";
        public const string HeartbeatInterval = "RabbitMQ.HeartbeatInterval";
        public const string NetworkRecoveryInterval = "RabbitMQ.NetworkRecoveryInterval";
    }
}
