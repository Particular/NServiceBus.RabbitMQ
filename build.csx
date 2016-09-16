#r "System.IO.Compression.FileSystem.dll"

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;

// external tools
var nugetUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe";
var nugetDirectory = ".nuget";
var nugetFile = "NuGet.exe";

var resharperZipUrl = "https://download.jetbrains.com/resharper/JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
var resharperZipDirectory = ".resharper";
var resharperZipFile = "JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
var inspectCodeFile = "inspectcode.exe";

// paths
var msBuild = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\MSBuild\\14.0\\Bin\\msbuild.exe");
var solution = ".\\src\\NServiceBus.RabbitMq.sln";
var dotSettings = ".\\src\\NServiceBus.RabbitMQ.sln.DotSettings";
var nunitConsole = ".\\src\\packages\\NUnit.ConsoleRunner.3.4.1\\tools\\nunit3-console.exe";
var unitTests = ".\\src\\NServiceBus.RabbitMQ.Tests\\bin\\Release\\NServiceBus.Transports.RabbitMQ.Tests.dll";
var acceptanceTests = ".\\src\\NServiceBus.RabbitMQ.AcceptanceTests\\bin\\Release\\NServiceBus.RabbitMQ.AcceptanceTests.dll";
var transportTests = ".\\src\\NServiceBus.Transport.RabbitMQ.TransportTests\\bin\\Release\\NServiceBus.Transport.RabbitMQ.TransportTests.dll";

// tasks
Console.WriteLine("Ensuring NuGet is present...");
EnsureFileCopiedFromCache(nugetUrl, nugetDirectory, nugetFile);

Console.WriteLine("Restoring NuGet packages...");
Cmd(Path.Combine(nugetDirectory, nugetFile), $"restore {solution}");

Console.WriteLine("Building solution...");
Cmd(msBuild, $"{solution} /property:Configuration=Release /nologo /maxcpucount /verbosity:minimal /nodeReuse:false");

Console.WriteLine("Ensuring Resharper command line tools zip is present...");
var resharperZipPath = EnsureFileCached(resharperZipUrl, resharperZipDirectory, resharperZipFile);

Console.WriteLine("Ensuring Resharper command line tools are unzipped...");
if (!File.Exists(Path.Combine(resharperZipDirectory, inspectCodeFile)))
{
    Console.WriteLine("Unzipping Resharper command line tools...");
    ZipFile.ExtractToDirectory(resharperZipPath, resharperZipDirectory);
}

Console.WriteLine("Inspecting solution...");
Cmd(Path.Combine(resharperZipDirectory, inspectCodeFile), $"--profile={dotSettings} {solution}");

Console.WriteLine("Running unit tests...");
Cmd(nunitConsole, unitTests);

Console.WriteLine("Running acceptance tests...");
Cmd(nunitConsole, acceptanceTests);

Console.WriteLine("Running transport tests...");
Cmd(nunitConsole, transportTests);

// dependencies
//
// build X
//  nuget restore X
//      download nuget X
// inspect X
//  unzip inspections X
//      download inspections X
//          build X
// unit test
//  nuget restore X
//  build X
// acceptance test
//  nuget restore X
//  build X
// transport test
//  nuget restore X
//  build X

public static void EnsureFileCopiedFromCache(string url, string directoryName, string fileName)
{
    var path = Path.Combine(directoryName, fileName);
    if (File.Exists(path))
    {
        return;
    }

    var cachePath = EnsureFileCached(url, directoryName, fileName);

    var directory = Path.GetDirectoryName(path);
    if (!Directory.Exists(directory))
    {
        Directory.CreateDirectory(directory);
    }

    File.Copy(cachePath, path);
}

public static string EnsureFileCached(string url, string directoryName, string fileName)
{
    var path = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), directoryName, fileName);
    if (!File.Exists(path))
    {
        var directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Console.WriteLine($"Downloading from {url}...");
        new WebClient().DownloadFile(url, path);
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
