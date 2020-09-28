﻿using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Settings;
using NServiceBus.Transport;
using NServiceBus.TransportTests;
using RabbitMQ.Client;

class ConfigureRabbitMQTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transactionMode)
    {
        var result = new TransportConfigurationResult();
        var transport = new RabbitMQTransport();

        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'RabbitMQTransport_ConnectionString' environment variable is not set.");
        }

        connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        queueBindings = settings.Get<QueueBindings>();

        new TransportExtensions<RabbitMQTransport>(settings).UseConventionalRoutingTopology();
        result.TransportInfrastructure = transport.Initialize(settings, connectionStringBuilder.ConnectionString);
        isTransportInitialized = true;
        result.PurgeInputQueueOnStartup = true;

        transportTransactionMode = result.TransportInfrastructure.TransactionMode;
        requestedTransactionMode = transactionMode;

        return result;
    }

    public Task Cleanup()
    {
        if (isTransportInitialized && transportTransactionMode >= requestedTransactionMode)
        {
            PurgeQueues(connectionStringBuilder, queueBindings);
        }

        return Task.FromResult(0);
    }

    static void PurgeQueues(DbConnectionStringBuilder connectionStringBuilder, QueueBindings queueBindings)
    {
        if (connectionStringBuilder == null)
        {
            return;
        }

        var connectionFactory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            UseBackgroundThreadsForIO = true
        };

        if (connectionStringBuilder.TryGetValue("username", out var value))
        {
            connectionFactory.UserName = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("password", out value))
        {
            connectionFactory.Password = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("virtualhost", out value))
        {
            connectionFactory.VirtualHost = value.ToString();
        }

        if (connectionStringBuilder.TryGetValue("host", out value))
        {
            connectionFactory.HostName = value.ToString();
        }
        else
        {
            throw new Exception("The connection string doesn't contain a value for 'host'.");
        }

        var queues = queueBindings.ReceivingAddresses.Concat(queueBindings.SendingAddresses);

        using (var connection = connectionFactory.CreateConnection("Test Queue Purger"))
        using (var channel = connection.CreateModel())
        {
            foreach (var queue in queues)
            {
                try
                {
                    channel.QueuePurge(queue);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to clear queue {0}: {1}", queue, ex);
                }
            }
        }
    }

    DbConnectionStringBuilder connectionStringBuilder;
    QueueBindings queueBindings;
    TransportTransactionMode transportTransactionMode;
    TransportTransactionMode requestedTransactionMode;
    bool isTransportInitialized;
}