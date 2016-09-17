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
var resharperZipPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/.resharper/JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
var inspectCodePath = ".resharper/inspectcode.exe";
var msBuild = $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}/MSBuild/14.0/Bin/msbuild.exe";
var solution = "./src/NServiceBus.RabbitMq.sln";
var dotSettings = "./src/NServiceBus.RabbitMQ.sln.DotSettings";
var nunitConsole = "./src/packages/NUnit.ConsoleRunner.3.4.1/tools/nunit3-console.exe";
var unitTests = "./src/NServiceBus.RabbitMQ.Tests/bin/Release/NServiceBus.Transports.RabbitMQ.Tests.dll";
var acceptanceTests = "./src/NServiceBus.RabbitMQ.AcceptanceTests/bin/Release/NServiceBus.RabbitMQ.AcceptanceTests.dll";
var transportTests = "./src/NServiceBus.Transport.RabbitMQ.TransportTests/bin/Release/NServiceBus.Transport.RabbitMQ.TransportTests.dll";



// targets
var targets = new Dictionary<string, Target>();

targets.Add(
    "default",
    new Target { Dependencies = new[] { "build", "inspect", "unit-test", "acceptance-test", "transport-test" } });

targets.Add(
    "build",
    new Target
    {
        Action = () => Cmd(
            msBuild, $"{solution} /property:Configuration=Release /nologo /maxcpucount /verbosity:minimal /nodeReuse:false"),
    });

targets.Add(
    "download-resharper-zip",
    new Target { Outputs = new[] { resharperZipPath }, Action = () => Download(resharperZipUrl, resharperZipPath), });

targets.Add(
    "unzip-resharper",
    new Target
    {
        Inputs = new[] { resharperZipPath },
        Outputs = new[] { inspectCodePath },
        Action = () => ZipFile.ExtractToDirectory(resharperZipPath, Path.GetDirectoryName(inspectCodePath)),
    });

targets.Add(
    "inspect",
    new Target
    {
        Dependencies = new[] { "build" },
        Inputs = new[] { inspectCodePath },
        Action = () => Cmd(inspectCodePath, $"--profile={dotSettings} {solution}"),
    });

targets.Add("unit-test", new Target { Dependencies = new[] { "build" }, Action = () => Cmd(nunitConsole, unitTests), });

targets.Add("acceptance-test", new Target { Dependencies = new[] { "build" }, Action = () => Cmd(nunitConsole, acceptanceTests), });

targets.Add("transport-test", new Target { Dependencies = new[] { "build" }, Action = () => Cmd(nunitConsole, transportTests), });



// boiler plate
public class Target
{
    public string[] Dependencies { get; set; }

    public string[] Inputs { get; set; }

    public string[] Outputs { get; set; }

    public Action Action { get; set; }
}

var names = Args.Any() ? Args : new[] { "default" };
var targetsAlreadyRun = new HashSet<string>();

foreach (var name in names)
{
    RunTarget(name, targets, targetsAlreadyRun);
}

Console.WriteLine($"Target(s) {string.Join(", ", names.Select(name => $"'{name}'"))} succeeded.");

public static void RunTarget(string name, Dictionary<string, Target> targets, HashSet<string> targetsAlreadyRun)
{
    Target target;
    if (!targets.TryGetValue(name, out target))
    {
        throw new InvalidOperationException($"Target '{name}' not found.");
    }

    targetsAlreadyRun.Add(name);

    var outputs = target.Outputs ?? Enumerable.Empty<string>();
    if (outputs.Any() && !outputs.Any(output => !File.Exists(output)))
    {
        Console.WriteLine($"Skipping target '{name}' since all outputs are present.");
        return;
    }

    var inputs = target.Inputs ?? Enumerable.Empty<string>();
    var dependencies = (target.Dependencies ?? Enumerable.Empty<string>())
        .Concat(
            targets
                .Where(t => (t.Value.Outputs ?? Enumerable.Empty<string>()).Intersect(inputs).Any())
                .Select(t => t.Key));

    if (dependencies.Any())
    {
        Console.WriteLine($"Running dependencies for target '{name}'...");
        foreach (var dependencyName in dependencies.Except(targetsAlreadyRun))
        {
            RunTarget(dependencyName, targets, targetsAlreadyRun);
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

public static void Download(string url, string path)
{
    var directory = Path.GetDirectoryName(path);
    if (!Directory.Exists(directory))
    {
        Directory.CreateDirectory(directory);
    }

    new WebClient().DownloadFile(url, path);
}
