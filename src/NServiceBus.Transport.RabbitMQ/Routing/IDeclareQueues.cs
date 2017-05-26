namespace NServiceBus.Transport.RabbitMQ
{
    using System.Collections.Generic;
    using global::RabbitMQ.Client;

    /// <summary>
    /// An extension for <see cref="IRoutingTopology"/> implementations which allows
    /// control over the declaration of queues as well as other initialization.
    /// The <see cref="DeclareAndInitialize(IModel, IEnumerable{string}, IEnumerable{string})"/> method will be called
    /// on all implementations instead of the <see cref="IRoutingTopology.Initialize(IModel, string)"/> method.
    /// </summary>
    public interface IDeclareQueues
    {
        /// <summary>
        /// Declares queues and performs any other initialization logic needed (e.g. creating exchanges and bindings).
        /// </summary>
        /// <param name="channel">The RabbitMQ channel to operate on.</param>
        /// <param name="receivingAddresses">
        /// The addresses of the queues to declare and perform initialization for, that this endpoint is receiving from.
        /// </param>
        /// <param name="sendingAddresses">
        /// The addresses of the queues to declare and perform initialization for, that this endpoint is sending to.
        /// </param>
        void DeclareAndInitialize(IModel channel, IEnumerable<string> receivingAddresses, IEnumerable<string> sendingAddresses);
    }
}
