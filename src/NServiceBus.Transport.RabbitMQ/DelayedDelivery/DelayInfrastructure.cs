namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Runtime.CompilerServices;
    using global::RabbitMQ.Client;

    static class DelayInfrastructure
    {
        const int maxNumberOfBitsToUse = 28;

        public const int MaxLevel = maxNumberOfBitsToUse - 1;
        public const int MaxDelayInSeconds = (1 << maxNumberOfBitsToUse) - 1;
        public const string DelayHeader = "NServiceBus.Transport.RabbitMQ.DelayInSeconds";
        public const string XDeathHeader = "x-death";
        public const string XFirstDeathExchangeHeader = "x-first-death-exchange";
        public const string XFirstDeathQueueHeader = "x-first-death-queue";
        public const string XFirstDeathReasonHeader = "x-first-death-reason";
        public const string DeliveryExchange = "nsb.v2.delay-delivery";

        public static string LevelName(int level) => $"nsb.v2.delay-level-{level:D2}";

        public static string BindingKey(string address) => $"#.{address}";

        public static void Build(IModel channel)
        {
            var bindingKey = "1.#";

            for (var level = MaxLevel; level >= 0; level--)
            {
                var currentLevel = LevelName(level);
                var nextLevel = LevelName(level - 1);

                channel.ExchangeDeclare(currentLevel, ExchangeType.Topic, true);

                var arguments = new Dictionary<string, object>
                {
                    { "x-queue-type", "quorum" },
                    { "x-dead-letter-strategy", "at-least-once" },
                    { "x-overflow", "reject-publish" },
                    { "x-message-ttl", Convert.ToInt64(Math.Pow(2, level)) * 1000 },
                    { "x-dead-letter-exchange", level > 0 ? nextLevel : DeliveryExchange }
                };

                channel.QueueDeclare(currentLevel, true, false, false, arguments);
                channel.QueueBind(currentLevel, currentLevel, bindingKey);

                bindingKey = "*." + bindingKey;
            }

            bindingKey = "0.#";

            for (var level = MaxLevel; level >= 1; level--)
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

            for (var level = MaxLevel; level >= 0; level--)
            {
                var name = LevelName(level);

                channel.QueueDelete(name);
                channel.ExchangeDelete(name);
            }
        }

        public static unsafe string CalculateRoutingKey(int delayInSeconds, string address, out int startingDelayLevel)
        {
            if (delayInSeconds < 0)
            {
                delayInSeconds = 0;
            }

            startingDelayLevel = 0;

            var addr = (IntPtr)Unsafe.AsPointer(ref startingDelayLevel);
            return string.Create((2 * MaxLevel) + 2 + address.Length, (address, delayInSeconds, addr), Action);

            static void Action(Span<char> span, (string address, int, IntPtr) state)
            {
                var (address, delayInSeconds, startingDelayLevelPtr) = state;

                var startingDelayLevel = 0;
                var mask = BitVector32.CreateMask();

                var bitVector = new BitVector32(delayInSeconds);

                var index = 0;
                for (var level = MaxLevel; level >= 0; level--)
                {
                    var bitFlag = bitVector[mask << level];
                    if (startingDelayLevel == 0 && bitFlag)
                    {
                        startingDelayLevel = level;
                    }

                    span[index++] = bitFlag ? '1' : '0';
                    span[index++] = '.';
                }

                address.AsSpan().CopyTo(span[index..]);

                Unsafe.Write(startingDelayLevelPtr.ToPointer(), startingDelayLevel);
            }
        }
    }
}
