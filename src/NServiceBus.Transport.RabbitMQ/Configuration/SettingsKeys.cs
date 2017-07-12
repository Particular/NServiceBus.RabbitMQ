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
        public const string DisableTimeoutManager = "RabbitMQ.DisableTimeoutManager";
        public const string RoutingTopologySupportsDelayedDelivery = "RabbitMQ.RoutingTopologySupportsDelayedDelivery";
        public const string PropagateBasicDeliverEventArgs = "RabbitMQ.PropagateBasicDeliverEventArgs";
    }
}
