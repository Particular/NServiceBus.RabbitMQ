#r "System.IO.Compression.FileSystem.dll"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

var cacheDirectory = Environment.GetEnvironmentVariable("LocalAppData");

var nugetUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe";
var nugetDirectory = ".nuget";
var nugetFile = "NuGet.exe";
var nugetPath = Path.Combine(nugetDirectory, nugetFile);
var nugetCachePath = Path.Combine(cacheDirectory, nugetPath);


var resharperZipUrl = "https://download.jetbrains.com/resharper/JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
var resharperZipDirectory = ".resharper-zip";
var resharperZipFile = "JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
var resharperZipPath = Path.Combine(cacheDirectory, resharperZipDirectory, resharperZipFile);

var resharperDirectory = ".resharper";
var resharperPath = Path.Combine(cacheDirectory, resharperDirectory);
var inspectCodeFile = "inspectcode.exe";
var inspectCodePath = Path.Combine(resharperDirectory, inspectCodeFile);
var inspectCodeCachePath = Path.Combine(cacheDirectory, inspectCodePath);


var msBuild = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\MSBuild\\14.0\\Bin\\msbuild.exe");
var solution = ".\\src\\NServiceBus.RabbitMq.sln";
var dotSettings = ".\\src\\NServiceBus.RabbitMQ.sln.DotSettings";
var nunitConsole = ".\\src\\packages\\NUnit.ConsoleRunner.3.4.1\\tools\\nunit3-console.exe";
var unitTests = ".\\src\\NServiceBus.RabbitMQ.Tests\\bin\\Release\\NServiceBus.Transports.RabbitMQ.Tests.dll";
var acceptanceTests = ".\\src\\NServiceBus.RabbitMQ.AcceptanceTests\\bin\\Release\\NServiceBus.RabbitMQ.AcceptanceTests.dll";
var transportTests = ".\\src\\NServiceBus.Transport.RabbitMQ.TransportTests\\bin\\Release\\NServiceBus.Transport.RabbitMQ.TransportTests.dll";

// targets
var targets = new Dictionary<string, Target>();

targets.Add(
    "default",
    new Target { DependsOn = new[] { "build", "inspect", "unit-test", "acceptance-test", "transport-test" } });

targets.Add(
    "get-nuget",
    new Target { Action = () => EnsureDownload(nugetUrl, nugetCachePath), });

targets.Add(
    "copy-nuget",
    new Target { DependsOn = new[] { "get-nuget" }, Action = () => EnsureCopy(nugetCachePath, nugetPath), });

targets.Add(
    "restore",
    new Target { DependsOn = new[] { "copy-nuget" }, Action = () => Cmd(nugetPath, $"restore {solution}"), });

targets.Add(
    "build",
    new Target
    {
        DependsOn = new[] { "restore" },
        Action = () => Cmd(msBuild, $"{solution} /property:Configuration=Release /nologo /maxcpucount /verbosity:minimal /nodeReuse:false"),
    });

targets.Add(
    "get-resharper-zip",
    new Target { Action = () => EnsureDownload(resharperZipUrl, resharperZipPath), });

targets.Add(
    "unzip-resharper",
    new Target
    {
        DependsOn = new[] { "get-resharper-zip" },
        Action = () =>
        {
            if (!File.Exists(inspectCodeCachePath))
            {
                Console.WriteLine($"Unzipping \"{resharperZipPath}\" to \"{resharperPath}\"...");
                ZipFile.ExtractToDirectory(resharperZipPath, resharperPath);
            }
        },
    });

targets.Add(
    "copy-resharper",
    new Target { DependsOn = new[] { "unzip-resharper" }, Action = () => EnsureCopy(inspectCodeCachePath, inspectCodePath), });

targets.Add(
    "inspect",
    new Target
    {
        DependsOn = new[] { "build", "copy-resharper" },
        Action = () => Cmd(inspectCodePath, $"--profile={dotSettings} {solution}"),
    });

targets.Add(
    "unit-test",
    new Target
    {
        DependsOn = new[] { "restore", "build" },
        Action = () => Cmd(nunitConsole, unitTests),
    });

targets.Add(
    "acceptance-test",
    new Target
    {
        DependsOn = new[] { "restore", "build" },
        Action = () => Cmd(nunitConsole, acceptanceTests),
    });

targets.Add(
    "transport-test",
    new Target
    {
        DependsOn = new[] { "restore", "build" },
        Action = () => Cmd(nunitConsole, transportTests),
    });

RunTargets(Args.Any() ? Args : new[] { "default" }, targets, new HashSet<string>());

public static void RunTargets(IEnumerable<string> name, Dictionary<string, Target> targets, HashSet<string> targetsRan)
{
    foreach (var key in name)
    {
        RunTarget(key, targets, targetsRan);
    }
}

public static void RunTarget(string name, Dictionary<string, Target> targets, HashSet<string> targetsRan)
{
    var target = targets[name];
    targetsRan.Add(name);
    foreach (var targetDependencyName in (target?.DependsOn ?? Enumerable.Empty<string>()).Except(targetsRan))
    {
        RunTarget(targetDependencyName, targets, targetsRan);
    }

    Console.WriteLine($"Running target \"{name}\"...");
    target.Action?.Invoke();
}

public static void EnsureDownload(string url, string path)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"Downloading from {url} to {path}...");
        new WebClient().DownloadFile(url, PreparePath(path));
    }
}

public static void EnsureCopy(string from, string to)
{
    if (!File.Exists(to))
    {
        File.Copy(from, PreparePath(to));
    }
}

public static string PreparePath(string path)
{
    var directory = Path.GetDirectoryName(path);
    if (!Directory.Exists(directory))
    {
        Directory.CreateDirectory(directory);
    }

    return path;
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

public class Target
{
    public string[] DependsOn { get; set; }

    public Action Action { get; set; }
}
