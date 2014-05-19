
namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using NUnit.Framework;

    public class When_running_concurrent_units_of_work_and_distributed_tx : RabbitMqContext
    {
        /// <summary>
        /// Reproduces https://github.com/Particular/NServiceBus.RabbitMQ/issues/26.
        /// When there are several units of work running simultaneously, handling concurrent units of
        /// work must not leak.
        /// </summary>
        [Test]
        public void Should_launch_all_TransactionCompleted_events()
        {
            var rounds = 20; //magic number, this should be enough to reproduce every time
            var count = 0;
            
            Parallel.For(0, rounds, i =>
            {
                using (var scope = GetTransaction())
                {
                    MakeTransactionDistributed();
                    unitOfWork.Add(model => Interlocked.Increment(ref count));
                    scope.Complete();
                }
            });

            Assert.AreEqual(rounds, count);
        }

        /// <summary>
        /// Turn on DTC by enlisting an extra durable transaction.
        /// </summary>
        static void MakeTransactionDistributed()
        {
            Transaction.Current.EnlistDurable(Guid.NewGuid(), new MockEnlistment(), EnlistmentOptions.None);
        }

        TransactionScope GetTransaction()
        {
            return new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromSeconds(15)
            });
        }

        /// <summary>
        /// Empty implementation of <see cref="IEnlistmentNotification"/>, can participate in transactions but
        /// does nothing.
        /// </summary>
        class MockEnlistment : IEnlistmentNotification
        {
            public void Prepare(PreparingEnlistment preparingEnlistment)
            {
                preparingEnlistment.Prepared();
            }

            public void Commit(Enlistment enlistment)
            {
                enlistment.Done();
            }

            public void Rollback(Enlistment enlistment)
            {
                enlistment.Done();
            }

            public void InDoubt(Enlistment enlistment)
            {
                enlistment.Done();
            }
        }    
    }
}
