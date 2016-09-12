#r "System.IO.Compression.FileSystem.dll"
#load "scripts/cmd.csx"
#load "scripts/download.csx"
#load "scripts/inspect.csx"
#load "src/packages/simple-targets-csx.5.0.0/simple-targets.csx"

using System;
using System.IO;
using System.IO.Compression;
using static SimpleTargets;

// locations
var resharperCltUrl = "https://download.jetbrains.com/resharper/JetBrains.ReSharper.CommandLineTools.2016.2.20160913.100041.zip";
var resharperCltPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/.resharper/JetBrains.ReSharper.CommandLineTools.2016.2.20160913.100041.zip";
var inspectCodePath = "./.resharper/inspectcode.exe";
var msBuild = $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}/MSBuild/14.0/Bin/msbuild.exe";
var solution = "./src/NServiceBus.RabbitMQ.sln";
var dotSettings = "./src/NServiceBus.RabbitMQ.sln.DotSettings";
var nunit = "./src/packages/NUnit.ConsoleRunner.3.4.1/tools/nunit3-console.exe";
var unitTests = "./src/NServiceBus.RabbitMQ.Tests/bin/Release/NServiceBus.Transports.RabbitMQ.Tests.dll";
var acceptanceTests = "./src/NServiceBus.RabbitMQ.AcceptanceTests/bin/Release/NServiceBus.RabbitMQ.AcceptanceTests.dll";
var transportTests = "./src/NServiceBus.Transport.RabbitMQ.TransportTests/bin/Release/NServiceBus.Transport.RabbitMQ.TransportTests.dll";

// targets
var targets = new TargetDictionary();

targets.Add("default", DependsOn("build", "inspect", "unit-test", "acceptance-test", "transport-test"));

targets.Add("build", () => Cmd(msBuild, $"{solution} /p:Configuration=Release /nologo /m /v:m /nr:false"));

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

targets.Add("unit-test", DependsOn("build"), () => Cmd(nunit, unitTests));

targets.Add("acceptance-test", DependsOn("build"), () => Cmd(nunit, acceptanceTests));

targets.Add("transport-test", DependsOn("build"), () => Cmd(nunit, transportTests));

Run(Args, targets);
