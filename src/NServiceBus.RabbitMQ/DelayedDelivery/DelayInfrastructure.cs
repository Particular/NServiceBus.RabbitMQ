﻿namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using Features;
    using global::RabbitMQ.Client;
    using Settings;

    static class DelayInfrastructure
    {
        const int maxNumberOfBitsToUse = 28;
        const int maxLevel = maxNumberOfBitsToUse - 1;

        public const int MaxDelayInSeconds = (1 << maxNumberOfBitsToUse) - 1;
        public const string DelayHeader = "NServiceBus.Transport.RabbitMQ.DelayInSeconds";
        public const string DeadLetteredMessageHeader = "x-death";
        public const string DeliveryExchange = "nsb.delay-delivery";

        public static string LevelName(int level) => $"nsb.delay-level-{level:D2}";

        public static string BindingKey(string address) => $"#.{address}";

        public static void Build(IModel channel)
        {
            var bindingKey = "1.#";

            for (var level = maxLevel; level >= 0; level--)
            {
                var currentLevel = LevelName(level);
                var nextLevel = LevelName(level - 1);

                channel.ExchangeDeclare(currentLevel, ExchangeType.Topic, true);

                var arguments = new Dictionary<string, object>
                {
                    { "x-queue-mode", "lazy" },
                    { "x-message-ttl", Convert.ToInt64(Math.Pow(2, level)) * 1000 },
                    { "x-dead-letter-exchange", level > 0 ? nextLevel : DeliveryExchange }
                };

                channel.QueueDeclare(currentLevel, true, false, false, arguments);
                channel.QueueBind(currentLevel, currentLevel, bindingKey);

                bindingKey = "*." + bindingKey;
            }

            bindingKey = "0.#";

            for (var level = maxLevel; level >= 1; level--)
            {
                var currentLevel = LevelName(level);
                var nextLevel = LevelName(level - 1);

                channel.ExchangeBind(nextLevel, currentLevel, bindingKey);

                bindingKey = "*." + bindingKey;
            }

            channel.ExchangeDeclare(DeliveryExchange, ExchangeType.Topic, true);
            channel.ExchangeBind(DeliveryExchange, LevelName(0), bindingKey);
        }

        public static void TearDown(IModel channel)
        {
            channel.ExchangeDelete(DeliveryExchange);

            for (var level = maxLevel; level >= 0; level--)
            {
                var name = LevelName(level);

                channel.QueueDelete(name);
                channel.ExchangeDelete(name);
            }
        }

        public static StartupCheckResult CheckForInvalidSettings(SettingsHolder settings)
        {
            var routingTopologySupportsDelayedDelivery = settings.GetOrDefault<bool>(SettingsKeys.RoutingTopologySupportsDelayedDelivery);
            var TimeoutManagerDisabled = settings.GetOrDefault<bool>(SettingsKeys.DisableTimeoutManager);
            var timeoutManagerFeatureActive = settings.GetOrDefault<FeatureState>(typeof(TimeoutManager).FullName) == FeatureState.Active;

            if (!routingTopologySupportsDelayedDelivery && (TimeoutManagerDisabled || !timeoutManagerFeatureActive))
            {
                return StartupCheckResult.Failed($"Cannot disable the timeout manager when the specified routing topology does not implement {nameof(ISupportDelayedDelivery)}.");
            }

            if (!TimeoutManagerDisabled && !timeoutManagerFeatureActive)
            {
                return StartupCheckResult.Failed("The timeout manager is not active, but the transport has not been properly configured for this. Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().DelayedDelivery().DisableTimeoutManager()' to ensure delayed messages can be sent properly.");
            }

            return StartupCheckResult.Success;
        }

        public static string CalculateRoutingKey(int delayInSeconds, string address, out int startingDelayLevel)
        {
            if (delayInSeconds < 0)
            {
                delayInSeconds = 0;
            }

            var bitArray = new BitArray(new[] { delayInSeconds });
            var sb = new StringBuilder();
            startingDelayLevel = 0;

            for (var level = maxLevel; level >= 0; level--)
            {
                if (startingDelayLevel == 0 && bitArray[level])
                {
                    startingDelayLevel = level;
                }

                sb.Append(bitArray[level] ? "1." : "0.");
            }

            sb.Append(address);

            return sb.ToString();
        }
    }
}
