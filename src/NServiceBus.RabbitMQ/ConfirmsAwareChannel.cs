namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Text.RegularExpressions;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;
    using Janitor;
    using Unicast.Queuing;

    [SkipWeaving]
    class ConfirmsAwareChannel:IDisposable
    {
        public IModel Channel { get; private set; }

        public ConfirmsAwareChannel(IConnection connection, bool usePublisherConfirms, TimeSpan maxWaitTimeForConfirms)
        {
            this.usePublisherConfirms = usePublisherConfirms;
            this.maxWaitTimeForConfirms = maxWaitTimeForConfirms;
            Channel = connection.CreateModel();

            if (usePublisherConfirms)
            {
                Channel.ConfirmSelect();
            }
        }

        public void Dispose()
        {
            if (!usePublisherConfirms)
            {
                return;
            }

            try
            {
                Channel.WaitForConfirmsOrDie(maxWaitTimeForConfirms);
            }
            catch (AlreadyClosedException ex)
            {
                if (ex.ShutdownReason != null && ex.ShutdownReason.ReplyCode == 404)
                {
                    var msg = ex.ShutdownReason.ReplyText;
                    var matches = Regex.Matches(msg, @"'([^' ]*)'");
                    var exchangeName = matches.Count > 0 && matches[0].Groups.Count > 1 ? Address.Parse(matches[0].Groups[1].Value) : null;
                    throw new QueueNotFoundException(exchangeName, "Exchange for the recipient does not exist", ex);
                }
                throw;
            }
        }

        readonly bool usePublisherConfirms;
        readonly TimeSpan maxWaitTimeForConfirms;
    }
}