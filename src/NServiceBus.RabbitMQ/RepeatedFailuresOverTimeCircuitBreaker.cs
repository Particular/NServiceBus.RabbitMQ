namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    class RepeatedFailuresOverTimeCircuitBreaker : IDisposable
    {
        public RepeatedFailuresOverTimeCircuitBreaker(string name, TimeSpan timeToWaitBeforeTriggering, Action<Exception> triggerAction, TimeSpan delayAfterFailure)
        {
            this.name = name;
            this.triggerAction = triggerAction;
            this.delayAfterFailure = delayAfterFailure;
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

        public Task Failure(Exception exception)
        {
            lastException = exception;
            var newValue = Interlocked.Increment(ref failureCount);

            if (newValue == 1)
            {
                timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
                Logger.WarnFormat("The circuit breaker for {0} is now in the armed state", name);
            }

            return Task.Delay(delayAfterFailure);
        }

        public void Dispose()
        {
            //Injected
        }

        void CircuitBreakerTriggered(object state)
        {
            if (Interlocked.Read(ref failureCount) > 0)
            {
                Logger.WarnFormat("The circuit breaker for {0} will now be triggered", name);
                triggerAction(lastException);
            }
        }

        static TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
        static ILog Logger = LogManager.GetLogger<RepeatedFailuresOverTimeCircuitBreaker>();
        string name;
        TimeSpan timeToWaitBeforeTriggering;
        Timer timer;
        Action<Exception> triggerAction;
        private readonly TimeSpan delayAfterFailure;
        long failureCount;
        Exception lastException;
    }
}