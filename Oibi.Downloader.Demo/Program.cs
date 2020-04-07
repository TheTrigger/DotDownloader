using Oibi.Download;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Oibi.Downloader.Demo
{
    internal class Program
    {
        private static async Task Main()
        {
            var settings = new FileDownloadSettings
            {
                RemoteResource = new Uri("https://speed.hetzner.de/100MB.bin"),
                LocalResource = new FileInfo(@"C:\Users\Fabio\Downloads\.downloader\100MB.bin")
            };

            var dl = new DotDownloder(settings);
            var task = dl.DownloadAsync();

            do
            {
                await Task.Delay(111);
                Console.Write($"Progress: {dl.Progress * 100:0.00}%\r");
            } while (dl.Progress != 1d);

            Console.WriteLine("DOWNLOAD COMPLETED");

            await task;
        }
    }
}