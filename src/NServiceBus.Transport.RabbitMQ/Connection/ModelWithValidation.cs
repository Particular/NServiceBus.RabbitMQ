using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NServiceBus.Transport.RabbitMQ
{
    class ModelWithValidation : IModel
    {
        IModel model;

        public ModelWithValidation(IModel model)
        {
            this.model = model;
        }

        public void Dispose()
        {
            model.Dispose();
        }

        public void Abort()
        {
            model.Abort();
        }

        public void Abort(ushort replyCode, string replyText)
        {
            model.Abort(replyCode, replyText);
        }

        public void BasicAck(ulong deliveryTag, bool multiple)
        {
            model.BasicAck(deliveryTag, multiple);
        }

        public void BasicCancel(string consumerTag)
        {
            model.BasicCancel(consumerTag);
        }

        public void BasicCancelNoWait(string consumerTag)
        {
            model.BasicCancelNoWait(consumerTag);
        }

        public string BasicConsume(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object> arguments,
            IBasicConsumer consumer)
        {
            return model.BasicConsume(queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer);
        }

        public BasicGetResult BasicGet(string queue, bool autoAck)
        {
            return model.BasicGet(queue, autoAck);
        }

        public void BasicNack(ulong deliveryTag, bool multiple, bool requeue)
        {
            model.BasicNack(deliveryTag, multiple, requeue);
        }

        public void BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body)
        {
            model.BasicPublish(exchange, routingKey, mandatory, basicProperties, body);
        }

        public void BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
        {
            model.BasicQos(prefetchSize, prefetchCount, global);
        }

        public void BasicRecover(bool requeue)
        {
            model.BasicRecover(requeue);
        }

        public void BasicRecoverAsync(bool requeue)
        {
            model.BasicRecoverAsync(requeue);
        }

        public void BasicReject(ulong deliveryTag, bool requeue)
        {
            model.BasicReject(deliveryTag, requeue);
        }

        public void Close()
        {
            model.Close();
        }

        public void Close(ushort replyCode, string replyText)
        {
            model.Close(replyCode, replyText);
        }

        public void ConfirmSelect()
        {
            model.ConfirmSelect();
        }

        public IBasicPublishBatch CreateBasicPublishBatch()
        {
            return model.CreateBasicPublishBatch();
        }

        public IBasicProperties CreateBasicProperties()
        {
            return model.CreateBasicProperties();
        }

        public void ExchangeBind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfShortStringIsTooLong(destination, nameof(destination));
            ThrowIfShortStringIsTooLong(source, nameof(source));
            ThrowIfShortStringIsTooLong(routingKey, nameof(routingKey));

            model.ExchangeBind(destination, source, routingKey, arguments);
        }

        public void ExchangeBindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfShortStringIsTooLong(destination, nameof(destination));
            ThrowIfShortStringIsTooLong(source, nameof(source));
            ThrowIfShortStringIsTooLong(routingKey, nameof(routingKey));

            model.ExchangeBindNoWait(destination, source, routingKey, arguments);
        }

        public void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfShortStringIsTooLong(exchange, nameof(exchange));
            ThrowIfShortStringIsTooLong(type, nameof(type));

            model.ExchangeDeclare(exchange, type, durable, autoDelete, arguments);
        }

        public void ExchangeDeclareNoWait(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfShortStringIsTooLong(exchange, nameof(exchange));
            ThrowIfShortStringIsTooLong(type, nameof(type));

            model.ExchangeDeclareNoWait(exchange, type, durable, autoDelete, arguments);
        }

        public void ExchangeDeclarePassive(string exchange)
        {
            ThrowIfShortStringIsTooLong(exchange, nameof(exchange));

            model.ExchangeDeclarePassive(exchange);
        }

        public void ExchangeDelete(string exchange, bool ifUnused)
        {
            model.ExchangeDelete(exchange, ifUnused);
        }

        public void ExchangeDeleteNoWait(string exchange, bool ifUnused)
        {
            model.ExchangeDeleteNoWait(exchange, ifUnused);
        }

        public void ExchangeUnbind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            model.ExchangeUnbind(destination, source, routingKey, arguments);
        }

        public void ExchangeUnbindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            model.ExchangeUnbindNoWait(destination, source, routingKey, arguments);
        }

        public void QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfShortStringIsTooLong(queue, nameof(queue));
            ThrowIfShortStringIsTooLong(exchange, nameof(exchange));
            ThrowIfShortStringIsTooLong(routingKey, nameof(routingKey));

            model.QueueBind(queue, exchange, routingKey, arguments);
        }

        public void QueueBindNoWait(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            ThrowIfShortStringIsTooLong(queue, nameof(queue));
            ThrowIfShortStringIsTooLong(exchange, nameof(exchange));
            ThrowIfShortStringIsTooLong(routingKey, nameof(routingKey));

            model.QueueBindNoWait(queue, exchange, routingKey, arguments);
        }

        public QueueDeclareOk QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfShortStringIsTooLong(queue, nameof(queue));

            return model.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
        }

        public void QueueDeclareNoWait(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            ThrowIfShortStringIsTooLong(queue, nameof(queue));
            
            model.QueueDeclareNoWait(queue, durable, exclusive, autoDelete, arguments);
        }

        public QueueDeclareOk QueueDeclarePassive(string queue)
        {
            ThrowIfShortStringIsTooLong(queue, nameof(queue));

            return model.QueueDeclarePassive(queue);
        }

        public uint MessageCount(string queue)
        {
            return model.MessageCount(queue);
        }

        public uint ConsumerCount(string queue)
        {
            return model.ConsumerCount(queue);
        }

        public uint QueueDelete(string queue, bool ifUnused, bool ifEmpty)
        {
            return model.QueueDelete(queue, ifUnused, ifEmpty);
        }

        public void QueueDeleteNoWait(string queue, bool ifUnused, bool ifEmpty)
        {
            model.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
        }

        public uint QueuePurge(string queue)
        {
            return model.QueuePurge(queue);
        }

        public void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            model.QueueUnbind(queue, exchange, routingKey, arguments);
        }

        public void TxCommit()
        {
            model.TxCommit();
        }

        public void TxRollback()
        {
            model.TxRollback();
        }

        public void TxSelect()
        {
            model.TxSelect();
        }

        public bool WaitForConfirms()
        {
            return model.WaitForConfirms();
        }

        public bool WaitForConfirms(TimeSpan timeout)
        {
            return model.WaitForConfirms(timeout);
        }

        public bool WaitForConfirms(TimeSpan timeout, out bool timedOut)
        {
            return model.WaitForConfirms(timeout, out timedOut);
        }

        public void WaitForConfirmsOrDie()
        {
            model.WaitForConfirmsOrDie();
        }

        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            model.WaitForConfirmsOrDie(timeout);
        }

        public int ChannelNumber => model.ChannelNumber;

        public ShutdownEventArgs CloseReason => model.CloseReason;

        public IBasicConsumer DefaultConsumer
        {
            get => model.DefaultConsumer;
            set => model.DefaultConsumer = value;
        }

        public bool IsClosed => model.IsClosed;

        public bool IsOpen => model.IsOpen;

        public ulong NextPublishSeqNo => model.NextPublishSeqNo;

        public TimeSpan ContinuationTimeout
        {
            get => model.ContinuationTimeout;
            set => model.ContinuationTimeout = value;
        }

        public event EventHandler<BasicAckEventArgs> BasicAcks
        {
            add => model.BasicAcks += value;
            remove => model.BasicAcks -= value;
        }

        public event EventHandler<BasicNackEventArgs> BasicNacks
        {
            add => model.BasicNacks += value;
            remove => model.BasicNacks -= value;
        }

        public event EventHandler<EventArgs> BasicRecoverOk
        {
            add => model.BasicRecoverOk += value;
            remove => model.BasicRecoverOk -= value;
        }

        public event EventHandler<BasicReturnEventArgs> BasicReturn
        {
            add => model.BasicReturn += value;
            remove => model.BasicReturn -= value;
        }

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add => model.CallbackException += value;
            remove => model.CallbackException -= value;
        }

        public event EventHandler<FlowControlEventArgs> FlowControl
        {
            add => model.FlowControl += value;
            remove => model.FlowControl -= value;
        }

        public event EventHandler<ShutdownEventArgs> ModelShutdown
        {
            add => model.ModelShutdown += value;
            remove => model.ModelShutdown -= value;
        }

        public static void ThrowIfShortStringIsTooLong(string name, string argumentName)
        {
            if (Encoding.UTF8.GetByteCount(name) > 255)
            {
                throw new ArgumentOutOfRangeException(argumentName, name, "Value exceeds the maximum allowed length of 255 bytes.");
            }
        }
    }
}