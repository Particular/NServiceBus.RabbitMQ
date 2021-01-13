namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    static class TaskEx
    {
        public static Task StartNew(object state, Action<object> action) => StartNew(state, action, TaskScheduler.Default);

        public static Task StartNew(object state, Action<object> action, TaskScheduler scheduler) => Task.Factory.StartNew(action, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
    }
}