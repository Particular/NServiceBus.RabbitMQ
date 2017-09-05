#r "System.IO.Compression.FileSystem.dll"
#load "scripts/broker.csx"
#load "scripts/cmd.csx"
#load "scripts/download.csx"
#load "scripts/inspect.csx"
#load "build-packages/simple-targets-csx.5.2.0/simple-targets.csx"

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using static SimpleTargets;

// locations
var resharperCltUrl = new Uri("https://download.jetbrains.com/resharper/JetBrains.ReSharper.CommandLineTools.2017.2.0.zip");
var resharperCltPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/.resharper/{resharperCltUrl.Segments.Last()}";
var inspectCodePath = $"./.resharper/{Path.GetFileNameWithoutExtension(resharperCltUrl.Segments.Last())}/inspectcode.exe";
var msBuild = $"{Environment.GetEnvironmentVariable("VS_INSTALL_PATH")}/MSBuild/15.0/Bin/MSBuild.exe";
var solution = "./src/NServiceBus.Transport.RabbitMQ.sln";
var dotSettings = "./src/NServiceBus.Transport.RabbitMQ.sln.DotSettings";
var nunit = "./build-packages/NUnit.ConsoleRunner.3.6.1/tools/nunit3-console.exe";
var unitTests = "./src/NServiceBus.Transport.RabbitMQ.Tests/bin/Release/net452/NServiceBus.Transport.RabbitMQ.Tests.dll";
var acceptanceTests = "./src/NServiceBus.Transport.RabbitMQ.AcceptanceTests/bin/Release/net452/NServiceBus.Transport.RabbitMQ.AcceptanceTests.dll";
var transportTests = "./src/NServiceBus.Transport.RabbitMQ.TransportTests/bin/Release/net452/NServiceBus.Transport.RabbitMQ.TransportTests.dll";

// targets
var targets = new TargetDictionary();

targets.Add("default", DependsOn("build", "inspect", "unit-test", "acceptance-test", "transport-test"));

targets.Add("restore", () => Cmd(msBuild, $"{solution} /p:Configuration=Release /t:restore"));

targets.Add("build", DependsOn("restore"), () => Cmd(msBuild, $"{solution} /p:Configuration=Release /nologo /m /v:m /nr:false"));

targets.Add(
    "download-resharper-clt",
    () => { if (!File.Exists(resharperCltPath)) Download(resharperCltUrl, resharperCltPath); });

targets.Add(
    "unzip-resharper-clt",
    DependsOn("download-resharper-clt"),
    () => { if (!File.Exists(inspectCodePath)) ZipFile.ExtractToDirectory(resharperCltPath, Path.GetDirectoryName(inspectCodePath)); });

targets.Add(
    "run-inspectcode",
    DependsOn("build", "unzip-resharper-clt"),
    () => Cmd(inspectCodePath, $"--profile={dotSettings} --output={solution}.inspections.xml {solution}"));

targets.Add(
    "inspect",
    DependsOn("run-inspectcode"),
    () =>
    {
        if (!Inspect($"{solution}.inspections.xml"))
        {
            Console.WriteLine("Inspection failed!");
            Environment.Exit(1);
        }
    });

targets.Add("delete-virtual-host", () => DeleteVirtualHost());

targets.Add("create-virtual-host", () => CreateVirtualHost());

targets.Add("add-user-to-virtual-host", () => AddUserToVirtualHost());

targets.Add("reset-virtual-host", DependsOn("delete-virtual-host", "create-virtual-host", "add-user-to-virtual-host"));

targets.Add("unit-test", DependsOn("build"), () => Cmd(nunit, $"--work={Path.GetDirectoryName(unitTests)} {unitTests}"));

targets.Add("acceptance-test", DependsOn("build"), () => Cmd(nunit, $"--work={Path.GetDirectoryName(acceptanceTests)} {acceptanceTests}"));

targets.Add("transport-test", DependsOn("build"), () => Cmd(nunit, $"--work={Path.GetDirectoryName(transportTests)} {transportTests}"));

Run(Args, targets);
