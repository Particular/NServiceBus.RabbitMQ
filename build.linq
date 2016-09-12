<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.IO.Compression.FileSystem.dll</Reference>
  <Namespace>System.IO.Compression</Namespace>
  <Namespace>System.Net</Namespace>
</Query>

void Main()
{
    // external tools
    var nugetUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe";
    var nugetDirectory = ".nuget";
    var nugetFile = "NuGet.exe";

    var resharperZipUrl = "https://download.jetbrains.com/resharper/JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
    var resharperZipDirectory = ".resharper";
    var resharperZipFile = "JetBrains.ReSharper.CommandLineTools.2016.2.20160912.114811.zip";
    var inspectCodeFile = "inspectcode.exe";

    // paths
    var msBuild = "%ProgramFiles(x86)%\\MSBuild\\14.0\\Bin\\msbuild.exe";
    var solution = ".\\src\\NServiceBus.RabbitMq.sln";
    var dotSettings = ".\\src\\NServiceBus.RabbitMQ.sln.DotSettings";
    var nunitConsole = ".\\src\\packages\\NUnit.ConsoleRunner.3.4.1\\tools\\nunit3-console.exe";
    var unitTests = ".\\src\\NServiceBus.RabbitMQ.Tests\\bin\\Release\\NServiceBus.Transports.RabbitMQ.Tests.dll";
    var acceptanceTests = ".\\src\\NServiceBus.RabbitMQ.AcceptanceTests\\bin\\Release\\NServiceBus.RabbitMQ.AcceptanceTests.dll";
    var transportTests = ".\\src\\NServiceBus.Transport.RabbitMQ.TransportTests\\bin\\Release\\NServiceBus.Transport.RabbitMQ.TransportTests.dll";

    // tasks
    "Ensuring NuGet is present...".Dump();
    EnsureFileCopiedFromCache(nugetUrl, nugetDirectory, nugetFile);

    "Restoring NuGet packages...".Dump();
    Util.Cmd($"{Path.Combine(nugetDirectory, nugetFile)} restore {solution}");

    "Building solution...".Dump();
    Util.Cmd($"\"{msBuild}\" {solution} /property:Configuration=Release /nologo /maxcpucount /verbosity:minimal /nodeReuse:false");

    "Ensuring Resharper command line tools zip is present...".Dump();
    var resharperZipPath = EnsureFileCached(resharperZipUrl, resharperZipDirectory, resharperZipFile);

    "Ensuring Resharper command line tools are unzipped...".Dump();
    if (!File.Exists(Path.Combine(resharperZipDirectory, inspectCodeFile)))
    {
        "Unzipping Resharper command line tools...".Dump();
        ZipFile.ExtractToDirectory(resharperZipPath, resharperZipDirectory);
    }

    "Inspecting solution...".Dump();
    Util.Cmd($"{Path.Combine(resharperZipDirectory, inspectCodeFile)} --profile={dotSettings} {solution}");

    "Running unit tests...".Dump();
    Util.Cmd($"{nunitConsole} {unitTests}");

    "Running acceptance tests...".Dump();
    Util.Cmd($"{nunitConsole} {acceptanceTests}");

    "Running transport tests...".Dump();
    Util.Cmd($"{nunitConsole} {transportTests}");

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
}

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

        $"Downloading from {url}...".Dump();
        new WebClient().DownloadFile(url, path);
    }

    return path;
}
