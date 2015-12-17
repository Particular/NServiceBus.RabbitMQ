namespace NServiceBus.Transports.RabbitMQ.Tests.ClusteringTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using global::RabbitMQ.Client;
    using NLog;
    using NServiceBus.Transports.RabbitMQ.Connection;
    using NUnit.Framework;
    using Settings;
    using Support;
    using Config;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using NServiceBus.Transports.RabbitMQ.Routing;

    public abstract class ClusteredTestContext
    {
        protected const string QueueName = "testreceiver";
        const string ErlangProcessName = "erl";
        protected static Logger Logger = LogManager.GetCurrentClassLogger();

        protected Dictionary<int, RabbitNode> RabbitNodes = new Dictionary<int, RabbitNode>
            {
                {1, new RabbitNode {Number = 1, Port = 5673, MgmtPort = 15673, ShouldBeRunning=true}},
                {2, new RabbitNode {Number = 2, Port = 5674, MgmtPort = 15674, ShouldBeRunning=true}},
                {3, new RabbitNode {Number = 3, Port = 5675, MgmtPort = 15675, ShouldBeRunning=true}}
            };

        readonly string rabbitMqCtl = "rabbitmqctl.bat";//make sure that you have the PATH environment variable setup
        readonly string rabbitMqServer = "rabbitmq-server.bat";//make sure that you have the PATH environment variable setup

        RabbitMqConnectionManager connectionManager;
        RabbitMqMessagePump messagePump;
        protected int[] erlangProcessesRunningBeforeTheTest;
        BlockingCollection<IncomingMessage> receivedMessages;
        RabbitMqMessageSender sender;
        IModel publishChannel;


        protected class RabbitNode
        {
            public static readonly string LocalHostName = RuntimeEnvironment.MachineName;
            public int MgmtPort;
            public int Number;
            public int Port;
            public bool ShouldBeRunning = true;

            /// <summary>
            ///     The FQ node name (eg rabbit1@JUSTINT).
            /// </summary>
            public string Name => $"rabbit{Number}@{LocalHostName}";
        }

        protected Process[] GetExistingErlangProcesses()
        {
            return Process.GetProcessesByName(ErlangProcessName);
        }

        void StartRabbitMqServer(RabbitNode node)
        {
            var envVars = new Dictionary<string, string>
                {
                    {"RABBITMQ_NODENAME", node.Name},
                    {"RABBITMQ_NODE_PORT", node.Port.ToString(CultureInfo.InvariantCulture)},
                    {"RABBITMQ_SERVER_START_ARGS", $"-rabbitmq_management listener [{{port,{node.MgmtPort}}}]"},
                };

            InvokeExternalProgram(rabbitMqServer, "-detached", envVars);
        }

        protected void InvokeRabbitMqCtl(RabbitNode node, string command)
        {
            var args = ($"-n {node.Name} {command}");
            InvokeExternalProgram(rabbitMqCtl, args);
        }

        static void InvokeExternalProgram(string program, string args, Dictionary<string, string> customEnvVars = null)
        {
            var startInfo = new ProcessStartInfo { UseShellExecute = false, RedirectStandardOutput = true, FileName = program, Arguments = args, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            var environmentVariables = startInfo.EnvironmentVariables;

            if (customEnvVars != null)
            {
                foreach (var customEnvVar in customEnvVars)
                {
                    Logger.Debug("Setting env var {0} to '{1}'", customEnvVar.Key, customEnvVar.Value);
                    if (environmentVariables.ContainsKey(customEnvVar.Key))
                    {
                        environmentVariables[customEnvVar.Key] = customEnvVar.Value;
                    }
                    else
                    {
                        environmentVariables.Add(customEnvVar.Key, customEnvVar.Value);
                    }
                }
            }

            var programName = Path.GetFileName(program);
            Logger.Debug("Running {0} with args: '{1}'", programName, args);
            var p = Process.Start(startInfo);
            var output = p.StandardOutput.ReadToEnd();
            output = output.Replace("\n", "  "); // replace line breaks for more terse logging output
            p.WaitForExit();
            Logger.Debug("Result: {0}", output);
        }

        [TestFixtureSetUp]
        public void TestContextFixtureSetup()
        {
            Logger.Trace("Running TestContextFixtureSetup");
            CaptureExistingErlangProcesses();
            StartUpRabbitNodes();
            ClusterRabbitNodes();
            SetHAPolicy();

            publishChannel = connectionManager.GetPublishConnection().CreateModel();

            Logger.Fatal("RabbitMQ cluster setup complete");
        }

        [TestFixtureTearDown]
        public void TestContextFixtureTearDown()
        {
            Logger.Trace("Running TestContextFixtureTearDown");

            messagePump?.Stop().GetAwaiter().GetResult();

            publishChannel.Close();
            publishChannel.Dispose();
            
            connectionManager.Dispose();

            var erlangProcessesToKill = GetExistingErlangProcesses().Select(p => p.Id).Except(erlangProcessesRunningBeforeTheTest).ToList();
            erlangProcessesToKill.ForEach(id => Process.GetProcessById(id).Kill());
        }

        void ClusterRabbitNodes()
        {
            ClusterRabbitNode(2, 1);
            ClusterRabbitNode(3, 1);
        }

        void SetHAPolicy()
        {
            const string command = @"set_policy ha-all ""^(?!amq\.).*"" ""{""""ha-mode"""": """"all""""}""";
            InvokeRabbitMqCtl(RabbitNodes[1], command);
        }

        void CaptureExistingErlangProcesses()
        {
            erlangProcessesRunningBeforeTheTest = GetExistingErlangProcesses().Select(p => p.Id).ToArray();
        }

        void StartUpRabbitNodes()
        {
            foreach (var node in RabbitNodes.Values.Where(node => node.ShouldBeRunning))
            {
                StartRabbitMqServer(node);
            }
        }

        void ClusterRabbitNode(int fromNodeNumber, int toNodeNumber, bool withReset = false)
        {
            var node = RabbitNodes[fromNodeNumber];
            var clusterToNode = RabbitNodes[toNodeNumber];
            InvokeRabbitMqCtl(node, "stop_app");
            if (withReset)
            {
                InvokeRabbitMqCtl(node, "reset");
            }
            InvokeRabbitMqCtl(node, $"join_cluster {clusterToNode.Name}");
            InvokeRabbitMqCtl(node, "start_app");
        }

        protected IncomingMessage SendAndReceiveAMessage()
        {
            OutgoingMessage message;
            return SendAndReceiveAMessage(out message);
        }

        protected IncomingMessage SendAndReceiveAMessage(out OutgoingMessage sentMessage)
        {
            Logger.Info("Sending a message");
            // TODO: Proper parameters for OutgoingMessage constructor
            var message = new OutgoingMessage(null, null, null);
            var transportOperations = new[]
            {
                new TransportOperation(message, new DispatchOptions(new UnicastAddressTag(QueueName), DispatchConsistency.Default)) 
            };
            sender.Dispatch(transportOperations, new ContextBag());
            sentMessage = message;
            var receivedMessage = WaitForMessage();
            return receivedMessage;
        }

        protected void SetupQueueAndSenderAndListener(string connectionString)
        {
            connectionManager = SetupRabbitMqConnectionManager(connectionString);
            EnsureRabbitQueueExists(QueueName);
            SetupMessageSender();
            SetupQueueListener(QueueName);
        }

        void SetupQueueListener(string queueName)
        {
            receivedMessages = new BlockingCollection<IncomingMessage>();
            messagePump = new RabbitMqMessagePump(
                connectionManager,
                new ConventionalRoutingTopology(true), 
                new FakeChannelProvider(publishChannel), 
                new ReceiveOptions(s => SecondaryReceiveSettings.Disabled(), new MessageConverter(), 1, 1000, true, "Cluster test") 
            );

            messagePump.Init(pushContext =>
                {
                    receivedMessages.Add(new IncomingMessage(pushContext.MessageId, pushContext.Headers, pushContext.BodyStream));
                    return TaskEx.Completed;
                }, 
                new CriticalError((endpoint, error, exception) => TaskEx.Completed), 
                new PushSettings(queueName, null, true, TransportTransactionMode.ReceiveOnly)
            ).GetAwaiter().GetResult();

            messagePump.Start(new PushRuntimeSettings());
        }

        void EnsureRabbitQueueExists(string queueName)
        {
            using (var channel = connectionManager.GetAdministrationConnection().CreateModel())
            {
                channel.QueueDeclare(queueName, true, false, false, null);
                channel.QueuePurge(queueName);
            }
        }

        void SetupMessageSender()
        {
            sender = new RabbitMqMessageSender(null, new FakeChannelProvider(publishChannel), null);
        }

        static RabbitMqConnectionManager SetupRabbitMqConnectionManager(string connectionString)
        {
            var config = new ConnectionStringParser(new SettingsHolder()).Parse(connectionString);
            //            config.OverrideClientProperties();
            var connectionFactory = new RabbitMqConnectionFactory(config);
            var newConnectionManager = new RabbitMqConnectionManager(connectionFactory, config);
            return newConnectionManager;
        }

        IncomingMessage WaitForMessage()
        {
            var waitTime = TimeSpan.FromSeconds(1);

            if (Debugger.IsAttached)
            {
                waitTime = TimeSpan.FromMinutes(10);
            }

            IncomingMessage transportMessage;
            receivedMessages.TryTake(out transportMessage, waitTime);

            return transportMessage;
        }

        protected string GetConnectionString()
        {
            var hosts = RabbitNodes.Values.OrderBy(n => n.Port).Select(n => $"{RabbitNode.LocalHostName}:{n.Port}");
            var connectionString = string.Concat("host=", string.Join(",", hosts));
            Logger.Info("Connection string is: '{0}'", connectionString);
            return connectionString;
        }

        protected void StopNode(int nodeNumber)
        {
            Logger.Warn("Stopping node {0}", nodeNumber);
            InvokeRabbitMqCtl(RabbitNodes[nodeNumber], "stop_app");
        }

        protected void StartNode(int nodeNumber)
        {
            Logger.Info("Starting node {0}", nodeNumber);
            InvokeRabbitMqCtl(RabbitNodes[nodeNumber], "start_app");
        }
    }
}