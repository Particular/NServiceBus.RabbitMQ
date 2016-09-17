#r "System.IO.Compression.FileSystem.dll"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;



// vars
var resharperZipUrl = "https://download.jetbrains.com/resharper/JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
var resharperZipDirectory = ".resharper-zip";
var resharperZipFile = "JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
var resharperZipPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), resharperZipDirectory, resharperZipFile);
var resharperDirectory = ".resharper";
var inspectCodeFile = "inspectcode.exe";
var inspectCodePath = Path.Combine(resharperDirectory, inspectCodeFile);

var msBuild = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MSBuild\\14.0\\Bin\\msbuild.exe");
var solution = ".\\src\\NServiceBus.RabbitMq.sln";
var dotSettings = ".\\src\\NServiceBus.RabbitMQ.sln.DotSettings";
var nunitConsole = ".\\src\\packages\\NUnit.ConsoleRunner.3.4.1\\tools\\nunit3-console.exe";
var unitTests = ".\\src\\NServiceBus.RabbitMQ.Tests\\bin\\Release\\NServiceBus.Transports.RabbitMQ.Tests.dll";
var acceptanceTests = ".\\src\\NServiceBus.RabbitMQ.AcceptanceTests\\bin\\Release\\NServiceBus.RabbitMQ.AcceptanceTests.dll";
var transportTests = ".\\src\\NServiceBus.Transport.RabbitMQ.TransportTests\\bin\\Release\\NServiceBus.Transport.RabbitMQ.TransportTests.dll";



// targets
var targets = new Dictionary<string, Target>();

targets.Add("default", new Target { Dependencies = new[] { "build", "inspect", "unit-test", "acceptance-test", "transport-test" } });

targets.Add(
    "build",
    new Target
    {
        Action = () => Cmd(msBuild, $"{solution} /property:Configuration=Release /nologo /maxcpucount /verbosity:minimal /nodeReuse:false"),
    });

targets.Add(
    "get-resharper-zip",
    new Target
    {
        Action = () =>
        {
            if (!File.Exists(resharperZipPath))
            {
                var directory = Path.GetDirectoryName(resharperZipPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Console.WriteLine($"Downloading from {resharperZipUrl} to {resharperZipPath}...");
                new WebClient().DownloadFile(resharperZipUrl, resharperZipPath);
            }
        },
    });

targets.Add(
    "unzip-resharper",
    new Target
    {
        Dependencies = new[] { "get-resharper-zip" },
        Action = () =>
        {
            if (!File.Exists(inspectCodePath))
            {
                Console.WriteLine($"Unzipping '{resharperZipPath}' to '{resharperDirectory}'...");
                ZipFile.ExtractToDirectory(resharperZipPath, resharperDirectory);
            }
        },
    });

targets.Add(
    "inspect",
    new Target
    {
        Dependencies = new[] { "build", "unzip-resharper" },
        Action = () => Cmd(inspectCodePath, $"--profile={dotSettings} {solution}"),
    });

targets.Add("unit-test", new Target { Dependencies = new[] { "build" }, Action = () => Cmd(nunitConsole, unitTests), });

targets.Add("acceptance-test", new Target { Dependencies = new[] { "build" }, Action = () => Cmd(nunitConsole, acceptanceTests), });

targets.Add("transport-test", new Target { Dependencies = new[] { "build" }, Action = () => Cmd(nunitConsole, transportTests), });

RunTargets(Args.Any() ? Args : new[] { "default" }, targets, new HashSet<string>());



// boiler plate
public class Target
{
    public string[] Dependencies { get; set; }

    public Action Action { get; set; }
}


public static void RunTargets(IEnumerable<string> names, Dictionary<string, Target> targets, HashSet<string> targetsAlreadyRun)
{
    foreach (var name in names)
    {
        RunTarget(name, targets, targetsAlreadyRun);
    }
}

public static void RunTarget(string name, Dictionary<string, Target> targets, HashSet<string> targetsAlreadyRun)
{
    Target target;
    if (!targets.TryGetValue(name, out target))
    {
        throw new InvalidOperationException($"Target '{name}' not found.");
    }

    targetsAlreadyRun.Add(name);

    if (target.Dependencies?.Any() ?? false)
    {
        Console.WriteLine($"Running dependencies for target '{name}'...");
        foreach (var targetDependencyName in target.Dependencies.Except(targetsAlreadyRun))
        {
            RunTarget(targetDependencyName, targets, targetsAlreadyRun);
        }
    }

    if (target.Action != null)
    {
        Console.WriteLine($"Running target '{name}'...");
        target.Action.Invoke();
    }
}

public static void Cmd(string fileName, string args)
{
    var info = new ProcessStartInfo
    {
        FileName = "\"" + fileName + "\"",
        Arguments = args,
        UseShellExecute = false,
    };

    Console.WriteLine($"Running {info.FileName} {info.Arguments}");

    using (var process = new Process())
    {
        process.StartInfo = info;
        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "The command exited with code {0}.",
                process.ExitCode.ToString(CultureInfo.InvariantCulture));

            throw new InvalidOperationException(message);
        }
    }
}
