namespace NServiceBus.Transports.RabbitMQ
{
    using NServiceBus.Settings;
    using NServiceBus.Support;

    class Callbacks
    {
        public Callbacks(ReadOnlySettings settings)
        {
            if (UseCallbackReceiver(settings))
            {
                mainQueue = settings.Get<string>("NServiceBus.LocalAddress");
                var callbackQueue = $"{mainQueue}.{RuntimeEnvironment.MachineName}";

                int maxConcurrencyForCallbackReceiver;
                if (!settings.TryGet(MaxConcurrencyForCallbackReceiver, out maxConcurrencyForCallbackReceiver))
                {
                    MaxConcurrency = 1;
                }

                QueueAddress = callbackQueue;
                Enabled = true;
            }
            else
            {
                Enabled = false;
            }
        }

        public string QueueAddress { get; }
        public bool Enabled { get; }
        public int MaxConcurrency { get; }

        public bool IsEnabledFor(string queueName)
        {
            return mainQueue != queueName;
        }

        static bool UseCallbackReceiver(ReadOnlySettings settings)
        {
            bool useCallbackReceiver;

            if (!settings.TryGet(UseCallbackReceiverSettingKey, out useCallbackReceiver))
            {
                useCallbackReceiver = true;
            }
            return useCallbackReceiver;
        }

        string mainQueue;
        public const string HeaderKey = "NServiceBus.RabbitMQ.CallbackQueue";
        public const string MaxConcurrencyForCallbackReceiver = "RabbitMQ.MaxConcurrencyForCallbackReceiver";
        public const string UseCallbackReceiverSettingKey = "RabbitMQ.UseCallbackReceiver";
    }
}