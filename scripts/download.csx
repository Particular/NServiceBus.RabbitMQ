using System;
using System.IO;
using System.Net;

public static void Download(Uri url, string path)
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
