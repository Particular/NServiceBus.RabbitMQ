namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Exceptions;
    using Janitor;
    using Logging;
    using NServiceBus.Transports.RabbitMQ.Connection;

    /// <summary>
    /// A connection that attempts to reconnect if the inner connection is closed.
    /// </summary>
    [SkipWeaving]
    class PersistentConnection: IConnection
    {
        public PersistentConnection(ClusterAwareConnectionFactory connectionFactory, TimeSpan retryDelay,string purpose)
        {
            this.connectionFactory = connectionFactory;
            this.retryDelay = retryDelay;
            this.purpose = purpose;

            TryToConnect(null);
        }

        public IModel CreateModel()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Rabbit server is not connected.");
            }
            return connection.CreateModel();
        }

       
        public void Close()
        {
            connection.ConnectionShutdown -= OnConnectionShutdown;
            connection.Close();
        }

        public void Close(int timeout)
        {
            connection.ConnectionShutdown -= OnConnectionShutdown;
            connection.Close(timeout);
        }


        void StartTryToConnect()
        {
            var timer = new Timer(TryToConnect);
            timer.Change(Convert.ToInt32(retryDelay.TotalMilliseconds), Timeout.Infinite);
        }

        void TryToConnect(object timer)
        {
            if (timer != null)
            {
                ((Timer) timer).Dispose();
            }

            Logger.Debug("Trying to connect");
            if (disposed)
            {
                return;
            }

            var success = false;
            try
            {
                connection = connectionFactory.CreateConnection(purpose);
                success = true;
            }
            catch (System.Net.Sockets.SocketException socketException)
            {
                LogException(socketException);
            }
            catch (BrokerUnreachableException brokerUnreachableException)
            {
                LogException(brokerUnreachableException);
            }

            if (success)
            {
                connection.ConnectionShutdown += OnConnectionShutdown;

                Logger.InfoFormat("Connected to RabbitMQ. Broker: '{0}', Port: {1}, VHost: '{2}'",
                                  connectionFactory.HostConfiguration.Host,
                                  connectionFactory.HostConfiguration.Port,
                                  connectionFactory.Configuration.VirtualHost);
            }
            else
            {
                Logger.ErrorFormat("Failed to connected to the Broker. Retrying in {0}", retryDelay);
                StartTryToConnect();
            }
        }
        bool IsConnected
        {
            get { return connection != null && connection.IsOpen && !disposed; }
        }

        void LogException(Exception exception)
        {
            Logger.ErrorFormat("Failed to connect to Broker: '{0}', Port: {1} VHost: '{2}'. " +
                               "ExceptionMessage: '{3}'",
                               connectionFactory.HostConfiguration.Host,
                               connectionFactory.HostConfiguration.Port,
                               connectionFactory.Configuration.VirtualHost,
                               exception.Message);
        }

        void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            if (disposed)
            {
                return;
            }
        
            Logger.InfoFormat("Disconnected from RabbitMQ Broker, reason: {0} , going to reconnect", reason);

            TryToConnect(null);
        }

        public void Abort()
        {
            connection.Abort();
        }

        public void Abort(ushort reasonCode, string reasonText)
        {
            connection.Abort(reasonCode, reasonText);
        }

        public void Abort(int timeout)
        {
            connection.Abort(timeout);
        }

        public void Abort(ushort reasonCode, string reasonText, int timeout)
        {
            connection.Abort(reasonCode, reasonText, timeout);
        }

        public AmqpTcpEndpoint Endpoint
        {
            get { return connection.Endpoint; }
        }

        public IProtocol Protocol
        {
            get { return connection.Protocol; }
        }

        public ushort ChannelMax
        {
            get { return connection.ChannelMax; }
        }

        public uint FrameMax
        {
            get { return connection.FrameMax; }
        }

        public ushort Heartbeat
        {
            get { return connection.Heartbeat; }
        }

        public IDictionary<string, object> ClientProperties
        {
            get { return connection.ClientProperties; }
        }

        public IDictionary<string, object> ServerProperties
        {
            get { return connection.ServerProperties; }
        }

        public AmqpTcpEndpoint[] KnownHosts
        {
            get { return connection.KnownHosts; }
        }

        public ShutdownEventArgs CloseReason
        {
            get { return connection.CloseReason; }
        }

        public bool IsOpen
        {
            get { return connection.IsOpen; }
        }

        public bool AutoClose
        {
            get { return connection.AutoClose; }
            set { connection.AutoClose = value; }
        }

        public IList<ShutdownReportEntry> ShutdownReport
        {
            get { return connection.ShutdownReport; }
        }

        public ConsumerWorkService ConsumerWorkService { get { return connection.ConsumerWorkService; } }

        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add { connection.ConnectionShutdown += value; }
            remove { connection.ConnectionShutdown -= value; }
        }

        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add { connection.CallbackException += value; }
            remove { connection.CallbackException -= value; }
        }

        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked
        {
            add { connection.ConnectionBlocked += value; }
            remove { connection.ConnectionBlocked -= value; }
        }

        public event EventHandler<EventArgs> ConnectionUnblocked
        {
            add { connection.ConnectionUnblocked += value; }
            remove { connection.ConnectionUnblocked -= value; }
        }


        public void Close(ushort reasonCode, string reasonText, int timeout)
        {
            connection.Close(reasonCode, reasonText, timeout);
        }

        public void Close(ushort reasonCode, string reasonText)
        {
            connection.Close(reasonCode, reasonText);
        }

        public void HandleConnectionBlocked(string reason)
        {
            connection.HandleConnectionBlocked(reason);
        }
        
        public void HandleConnectionUnblocked()
        {
            connection.HandleConnectionUnblocked();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (connection == null)
            {
                return;
            }

            try
            {
                if (connection.IsOpen)
                {
                    Close(5000);
                }

                connection.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error("Failure when disposing RabbitMq connection", ex);
            }

            connection = null;
            disposed = true;
        }


        bool disposed;
        IConnection connection;
        readonly ClusterAwareConnectionFactory connectionFactory;
        readonly TimeSpan retryDelay;
        readonly string purpose;

        static readonly ILog Logger = LogManager.GetLogger(typeof (RabbitMqConnectionManager));
        public EndPoint LocalEndPoint { get { return connection.LocalEndPoint; }}
        public EndPoint RemoteEndPoint { get { return connection.RemoteEndPoint; } }
        public int LocalPort { get { return connection.LocalPort; } }
        public int RemotePort { get { return connection.RemotePort; } }
    }
}