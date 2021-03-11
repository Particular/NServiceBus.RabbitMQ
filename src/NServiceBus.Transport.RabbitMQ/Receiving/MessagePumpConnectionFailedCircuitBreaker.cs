namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Threading;
    using Logging;

    sealed class MessagePumpConnectionFailedCircuitBreaker : IDisposable
    {
        public MessagePumpConnectionFailedCircuitBreaker(string name, TimeSpan timeToWaitBeforeTriggering, Action<string, Exception> criticalErrorAction)
        {
            this.name = name;
            this.criticalErrorAction = criticalErrorAction;
            this.timeToWaitBeforeTriggering = timeToWaitBeforeTriggering;

            timer = new Timer(CircuitBreakerTriggered);
        }

        public void Success()
        {
            var oldValue = Interlocked.Exchange(ref failureCount, 0);

            if (oldValue == 0)
            {
                return;
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.InfoFormat("The circuit breaker for {0} is now disarmed", name);
        }

        public void Failure(Exception exception)
        {
            lastException = exception;
            var newValue = Interlocked.Increment(ref failureCount);

            if (newValue == 1)
            {
                timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
                Logger.WarnFormat("The circuit breaker for {0} is now in the armed state", name);
            }
        }

        public void Dispose()
        {
            timer?.Dispose();
        }

        void CircuitBreakerTriggered(object state)
        {
            if (Interlocked.Read(ref failureCount) > 0)
            {
                Logger.WarnFormat("The circuit breaker for {0} will now be triggered", name);
                criticalErrorAction($"{name} connection to the broker has failed.", lastException);
            }
        }

        static TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
        static ILog Logger = LogManager.GetLogger<MessagePumpConnectionFailedCircuitBreaker>();
        string name;
        TimeSpan timeToWaitBeforeTriggering;
        Timer timer;
        Action<string, Exception> criticalErrorAction;
        long failureCount;
        Exception lastException;
    }
}