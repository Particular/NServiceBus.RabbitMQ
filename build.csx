#r "System.IO.Compression.FileSystem.dll"
#load "scripts/broker.csx"
#load "scripts/cmd.csx"
#load "scripts/download.csx"
#load "scripts/inspect.csx"
#load "src/packages/simple-targets-csx.5.2.0/simple-targets.csx"

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using static SimpleTargets;

// locations
var resharperCltUrl = new Uri("https://download.jetbrains.com/resharper/JetBrains.ReSharper.CommandLineTools.2016.3.20170126.124346.zip");
var resharperCltPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/.resharper/{resharperCltUrl.Segments.Last()}";
var inspectCodePath = $"./.resharper/{Path.GetFileNameWithoutExtension(resharperCltUrl.Segments.Last())}/inspectcode.exe";
var nuget = "src/.nuget/v4.0.0/NuGet.exe";
var msBuild = $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}/Microsoft Visual Studio/2017/Professional/MSBuild/15.0/Bin/MSBuild.exe";
var solution = "./src/NServiceBus.RabbitMQ.sln";
var dotSettings = "./src/NServiceBus.RabbitMQ.sln.DotSettings";
var nunit = "./src/packages/NUnit.ConsoleRunner.3.6.1/tools/nunit3-console.exe";
var unitTests = "./src/NServiceBus.RabbitMQ.Tests/bin/Release/NServiceBus.Transports.RabbitMQ.Tests.dll";
var acceptanceTests = "./src/NServiceBus.RabbitMQ.AcceptanceTests/bin/Release/NServiceBus.RabbitMQ.AcceptanceTests.dll";
var transportTests = "./src/NServiceBus.Transport.RabbitMQ.TransportTests/bin/Release/NServiceBus.Transport.RabbitMQ.TransportTests.dll";

// targets
var targets = new TargetDictionary();

targets.Add("default", DependsOn("build", "inspect", "unit-test", "acceptance-test", "transport-test"));

targets.Add("restore", () => Cmd(nuget, $"restore {solution}"));

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
