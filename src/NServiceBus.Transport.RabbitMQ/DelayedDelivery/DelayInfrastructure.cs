﻿#nullable enable

namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;

    static class DelayInfrastructure
    {
        public const int MaxNumberOfBitsToUse = 28;

        public const int MaxLevel = MaxNumberOfBitsToUse - 1;
        public const int MaxDelayInSeconds = (1 << MaxNumberOfBitsToUse) - 1;
        public const string DelayHeader = "NServiceBus.Transport.RabbitMQ.DelayInSeconds";
        public const string XDeathHeader = "x-death";
        public const string XFirstDeathExchangeHeader = "x-first-death-exchange";
        public const string XFirstDeathQueueHeader = "x-first-death-queue";
        public const string XFirstDeathReasonHeader = "x-first-death-reason";
        public const string DeliveryExchange = "nsb.v2.delay-delivery";

        public static string LevelName(int level) => $"nsb.v2.delay-level-{level:D2}";

        public static string BindingKey(string address) => $"#.{address}";

        public static async Task Build(IChannel channel, CancellationToken cancellationToken = default)
        {
            var bindingKey = "1.#";

            for (var level = MaxLevel; level >= 0; level--)
            {
                var currentLevel = LevelName(level);
                var nextLevel = LevelName(level - 1);

                await channel.ExchangeDeclareAsync(currentLevel, ExchangeType.Topic, true, cancellationToken: cancellationToken).ConfigureAwait(false);

                var arguments = new Dictionary<string, object?>
                {
                    { "x-queue-type", "quorum" },
                    { "x-dead-letter-strategy", "at-least-once" },
                    { "x-overflow", "reject-publish" },
                    { "x-message-ttl", Convert.ToInt64(Math.Pow(2, level)) * 1000 },
                    { "x-dead-letter-exchange", level > 0 ? nextLevel : DeliveryExchange }
                };

                await channel.QueueDeclareAsync(currentLevel, true, false, false, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
                await channel.QueueBindAsync(currentLevel, currentLevel, bindingKey, cancellationToken: cancellationToken).ConfigureAwait(false);

                bindingKey = "*." + bindingKey;
            }

            bindingKey = "0.#";

            for (var level = MaxLevel; level >= 1; level--)
            {
                var currentLevel = LevelName(level);
                var nextLevel = LevelName(level - 1);

                await channel.ExchangeBindAsync(nextLevel, currentLevel, bindingKey, cancellationToken: cancellationToken).ConfigureAwait(false);

                bindingKey = "*." + bindingKey;
            }

            await channel.ExchangeDeclareAsync(DeliveryExchange, ExchangeType.Topic, true, cancellationToken: cancellationToken).ConfigureAwait(false);
            await channel.ExchangeBindAsync(DeliveryExchange, LevelName(0), bindingKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public static async Task TearDown(IChannel channel, CancellationToken cancellationToken = default)
        {
            await channel.ExchangeDeleteAsync(DeliveryExchange, cancellationToken: cancellationToken).ConfigureAwait(false);

            for (var level = MaxLevel; level >= 0; level--)
            {
                var name = LevelName(level);
                await channel.QueueDeleteAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false);
                await channel.ExchangeDeleteAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        public static unsafe string CalculateRoutingKey(int delayInSeconds, string address, out int startingDelayLevel)
        {
            if (delayInSeconds < 0)
            {
                delayInSeconds = 0;
            }

            startingDelayLevel = 0;
            // Pinning the address of the startingDelayLevel so that we can safely write back to the address of the local.
            // This trickery is done because when string.Create returns, there is no way to pass state outside of the lambda
            // without doing a closure over a local variable, which would create DisplayClass allocations for every call.
            fixed (int* pinnedStartingDelayLevel = &startingDelayLevel)
            {
                var startingDelayLevelPtr = (nint)pinnedStartingDelayLevel;

                // The length of the string is determined by the max level, taking into account the number of dots, the address length
                // and additional space since we are zero based.
                return string.Create((2 * MaxLevel) + 2 + address.Length, (delayInSeconds, address, startingDelayLevelPtr), Action);

                static void Action(Span<char> span, (int, string, nint) state)
                {
                    var (delayInSeconds, address, startingDelayLevelPtr) = state;

                    var startingDelayLevel = 0;

                    var index = 0;
                    for (var level = MaxLevel; level >= 0; level--)
                    {
                        bool bitSet = ((delayInSeconds >> level) & 1) != 0;
                        if (startingDelayLevel == 0 && bitSet)
                        {
                            startingDelayLevel = level;
                        }

                        span[index++] = bitSet ? '1' : '0';
                        span[index++] = '.';
                    }

                    address.AsSpan().CopyTo(span[index..]);

                    // Write back the startingDelayLevel to the address of the local
                    Unsafe.Write(startingDelayLevelPtr.ToPointer(), startingDelayLevel);
                }
            }
        }
    }
}
