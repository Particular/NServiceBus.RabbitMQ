#r "System.IO.Compression.FileSystem.dll"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;



// locations
var resharperCltUrl = "https://download.jetbrains.com/resharper/JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
var resharperCltPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/.resharper/JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
var inspectCodePath = "./.resharper/inspectcode.exe";
var msBuild = $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}/MSBuild/14.0/Bin/msbuild.exe";
var solution = "./src/NServiceBus.RabbitMq.sln";
var dotSettings = "./src/NServiceBus.RabbitMQ.sln.DotSettings";
var nunit = "./src/packages/NUnit.ConsoleRunner.3.4.1/tools/nunit3-console.exe";
var unitTests = "./src/NServiceBus.RabbitMQ.Tests/bin/Release/NServiceBus.Transports.RabbitMQ.Tests.dll";
var acceptanceTests = "./src/NServiceBus.RabbitMQ.AcceptanceTests/bin/Release/NServiceBus.RabbitMQ.AcceptanceTests.dll";
var transportTests = "./src/NServiceBus.Transport.RabbitMQ.TransportTests/bin/Release/NServiceBus.Transport.RabbitMQ.TransportTests.dll";



// targets
var targets = new Dictionary<string, Target>();

targets.Add("default", new Target { DependOn = new[] { "build", "inspect", "unit-test", "acceptance-test", "transport-test" } });

targets.Add("build", new Target { Do = () => Cmd(msBuild, $"{solution} /p:Configuration=Release /nologo /m /v:m /nr:false"), });

targets.Add(
    "download-resharper-clt",
    new Target { Outputs = new[] { resharperCltPath }, Do = () => Download(resharperCltUrl, resharperCltPath), });

targets.Add(
    "unzip-resharper-clt",
    new Target
    {
        Inputs = new[] { resharperCltPath },
        Outputs = new[] { inspectCodePath },
        Do = () => ZipFile.ExtractToDirectory(resharperCltPath, Path.GetDirectoryName(inspectCodePath)),
    });

targets.Add(
    "inspect",
    new Target
    {
        DependOn = new[] { "build" },
        Inputs = new[] { inspectCodePath },
        Do = () => Cmd(inspectCodePath, $"--profile={dotSettings} {solution}"),
    });

targets.Add("unit-test", new Target { DependOn = new[] { "build" }, Do = () => Cmd(nunit, unitTests), });

targets.Add("acceptance-test", new Target { DependOn = new[] { "build" }, Do = () => Cmd(nunit, acceptanceTests), });

targets.Add("transport-test", new Target { DependOn = new[] { "build" }, Do = () => Cmd(nunit, transportTests), });



// target running boiler plate
Run(Args, targets);

public class Target
{
    public string[] DependOn { get; set; }

    public string[] Inputs { get; set; }

    public string[] Outputs { get; set; }

    public Action Do { get; set; }
}

public static void Run(IList<string> args, IDictionary<string, Target> targets)
{
    var argsOptions = args.Where(arg => arg.StartsWith("-", StringComparison.Ordinal)).ToList();
    var argsTargets = args.Except(argsOptions).ToList();

    foreach (var option in argsOptions)
    {
        switch (option)
        {
            case "-H":
            case "-h":
            case "-?":
                Console.WriteLine("Usage: <script-runner> build.csx [<options>] [<targets>]");
                Console.WriteLine();
                Console.WriteLine("script-runner: A C# script runner. E.g. csi.exe.");
                Console.WriteLine();
                Console.WriteLine("options:");
                Console.WriteLine(" -T      Display the targets, then exit");
                Console.WriteLine();
                Console.WriteLine("targets: A list of targets to run. If not specified, 'default' target will be run.");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  csi.exe build.csx");
                Console.WriteLine("  csi.exe build.csx -T");
                Console.WriteLine("  csi.exe build.csx test package");
                return;
            case "-T":
                foreach (var target in targets)
                {
                    Console.WriteLine(target.Key);
                }

                return;
            default:
                Console.WriteLine($"Unknown option '{option}'.");
                return;
        }
    }

    var targetNames = argsTargets.Any() ? argsTargets : new List<string> { "default" };
    var targetsRan = new HashSet<string>();

    targetNames.ForEach(name => RunTarget(name, targets, targetsRan));

    Console.WriteLine($"Target(s) {string.Join(", ", targetNames.Select(name => $"'{name}'"))} succeeded.");
}

public static void RunTarget(string name, IDictionary<string, Target> targets, ISet<string> targetsRan)
{
    Target target;
    if (!targets.TryGetValue(name, out target))
    {
        throw new InvalidOperationException($"Target '{name}' not found.");
    }

    targetsRan.Add(name);

    var outputs = target.Outputs ?? Enumerable.Empty<string>();
    if (outputs.Any() && !outputs.Any(output => !File.Exists(output)))
    {
        Console.WriteLine($"Skipping target '{name}' since all outputs are present.");
        return;
    }

    var inputs = target.Inputs ?? Enumerable.Empty<string>();
    var dependencies = (target.DependOn ?? Enumerable.Empty<string>()).Concat(
        targets.Where(t => (t.Value.Outputs ?? Enumerable.Empty<string>()).Intersect(inputs).Any()).Select(t => t.Key));

    if (dependencies.Any())
    {
        Console.WriteLine($"Running dependencies for target '{name}'...");
        foreach (var dependencyName in dependencies.Except(targetsRan))
        {
            RunTarget(dependencyName, targets, targetsRan);
        }
    }

    if (target.Do != null)
    {
        Console.WriteLine($"Running target '{name}'...");
        target.Do.Invoke();
    }
}



// target writing boiler plate
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

    var lastUpdate = DateTime.Now;
    var lastPercentage = 0;
    using (var client = new WebClient())
    {
        client.DownloadProgressChanged += (sender, e) =>
        {
            var now = DateTime.Now;
            if (now - lastUpdate >= TimeSpan.FromSeconds(1) && e.ProgressPercentage >= lastPercentage + 10)
            {
                lastUpdate = now;
                lastPercentage = (int)Math.Round(e.ProgressPercentage / 10d, 0) * 10;
                Console.WriteLine($"\rDownloading '{url}' ({lastPercentage}%)...");
            }
        };

        client.DownloadFileCompleted += (sender, e) => Console.WriteLine($"\rDownloaded '{url}'.");

        client.DownloadFileTaskAsync(url, path).GetAwaiter().GetResult();
    }
}
