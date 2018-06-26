#r "System.IO.Compression.FileSystem.dll"
#r "build-packages/Bullseye.1.0.0-rc.4/lib/netstandard2.0/Bullseye.dll"
#r "build-packages/SimpleExec.2.2.0/lib/netstandard2.0/SimpleExec.dll"
#load "scripts/broker.csx"
#load "scripts/download.csx"
#load "scripts/inspect.csx"

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using static Bullseye.Targets;
using static SimpleExec.Command;

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
Add("default", DependsOn("build", "inspect", "unit-test", "acceptance-test", "transport-test"));

Add("restore", () => Run(msBuild, $"{solution} /p:Configuration=Release /t:restore"));

Add("build", DependsOn("restore"), () => Run(msBuild, $"{solution} /p:Configuration=Release /nologo /m /v:m /nr:false"));

Add(
    "download-resharper-clt",
    () => { if (!File.Exists(resharperCltPath)) Download(resharperCltUrl, resharperCltPath); });

Add(
    "unzip-resharper-clt",
    DependsOn("download-resharper-clt"),
    () => { if (!File.Exists(inspectCodePath)) ZipFile.ExtractToDirectory(resharperCltPath, Path.GetDirectoryName(inspectCodePath)); });

Add(
    "run-inspectcode",
    DependsOn("build", "unzip-resharper-clt"),
    () => Run(inspectCodePath, $"--profile={dotSettings} --output={solution}.inspections.xml {solution}"));

Add(
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

Add("delete-virtual-host", () => DeleteVirtualHost());

Add("create-virtual-host", () => CreateVirtualHost());

Add("add-user-to-virtual-host", () => AddUserToVirtualHost());

Add("reset-virtual-host", DependsOn("delete-virtual-host", "create-virtual-host", "add-user-to-virtual-host"));

Add("unit-test", DependsOn("build"), () => Run(nunit, $"--work={Path.GetDirectoryName(unitTests)} {unitTests}"));

Add("acceptance-test", DependsOn("build"), () => Run(nunit, $"--work={Path.GetDirectoryName(acceptanceTests)} {acceptanceTests}"));

Add("transport-test", DependsOn("build"), () => Run(nunit, $"--work={Path.GetDirectoryName(transportTests)} {transportTests}"));

Run(Args);
