namespace NServiceBus.Transport.RabbitMQ
{
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Exceptions;

    static class QueueHelper
    {
        public static bool QueueExists(IConnection connection, string queueAddress)
        {
            try
            {
                // create temporary channel as the channel will be faulted if the queue does not exist.
                using (var tempChannel = connection.CreateModel())
                {
                    // check queue existence via DeclarePassive to allow the destination queue to be either a quorum or a classic queue without failing the operation.
                    tempChannel.QueueDeclarePassive(queueAddress);
                    return true;
                }
            }
            catch (OperationInterruptedException e) when (e.ShutdownReason.ReplyCode == 404)
            {
                // queue does not exist
                return false;
            }
        }
    }
}