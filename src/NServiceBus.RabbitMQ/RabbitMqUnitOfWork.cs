namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Transactions;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;
    using Unicast.Queuing;

    public class RabbitMqUnitOfWork
    {
        public IManageRabbitMqConnections ConnectionManager { get; set; }

        /// <summary>
        ///     If set to true publisher confirms will be used to make sure that messages are acked by the broker before considered
        ///     to be published
        /// </summary>
        public bool UsePublisherConfirms { get; set; }

        /// <summary>
        ///     The maximum time to wait for all publisher confirms to be received
        /// </summary>
        public TimeSpan MaxWaitTimeForConfirms { get; set; }

        public void Add(Action<IModel> action)
        {
            var transaction = Transaction.Current;

            if (transaction == null)
            {
                ExecuteRabbitMqActions(new[]
                {
                    action
                });

                return;
            }

            var transactionId = transaction.TransactionInformation.LocalIdentifier;
            OutstandingOperations.AddOrUpdate(transactionId, s =>
            {
                transaction.TransactionCompleted += ExecuteActionsAgainstRabbitMq;
                return new List<Action<IModel>>
                {
                    action
                };
            }, (s, list) =>
            {
                list.Add(action);
                return list;
            });
        }

        void ExecuteActionsAgainstRabbitMq(object sender, TransactionEventArgs transactionEventArgs)
        {
            transactionEventArgs.Transaction.TransactionCompleted -= ExecuteActionsAgainstRabbitMq;

            var transactionInfo = transactionEventArgs.Transaction.TransactionInformation;
            var transactionId = transactionInfo.LocalIdentifier;

            if (transactionInfo.Status != TransactionStatus.Committed)
            {
                return;
            }

            IList<Action<IModel>> actions;
            OutstandingOperations.TryRemove(transactionId, out actions);

            if (!actions.Any())
            {
                return;
            }

            ExecuteRabbitMqActions(actions);
        }

        void ExecuteRabbitMqActions(IEnumerable<Action<IModel>> actions)
        {
            using (var channel = ConnectionManager.GetPublishConnection().CreateModel())
            {
                if (UsePublisherConfirms)
                {
                    channel.ConfirmSelect();
                }

                foreach (var action in actions)
                {
                    action(channel);
                }
                try
                {
                    channel.WaitForConfirmsOrDie(MaxWaitTimeForConfirms);
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
        }

        static ConcurrentDictionary<string, IList<Action<IModel>>> OutstandingOperations = new ConcurrentDictionary<string, IList<Action<IModel>>>();
    }
}