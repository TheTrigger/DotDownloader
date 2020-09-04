using Oibi.Download;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Oibi.Downloader.Demo
{
    internal class Program
    {
        private static async Task Main()
        {
            var settings = new FileDownloadSettings
            {
                RemoteResource = new Uri("https://www.gigainlab.com/test.iso"),
                LocalResource = new FileInfo(@"C:\Users\Fabio\Downloads\.downloader\alp.iso")
            };

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "hello");

            var dl = new DotDownloader(settings, httpClient);
            var task = dl.DownloadAsync(CancellationToken.None);

            do
            {
                await Task.Delay(111);
                Console.Write($"Progress: {dl.Progress * 100:00.00}%\r\n");
            } while (dl.IsDownloading);

            Console.WriteLine("\nDOWNLOAD COMPLETED");

            await task;
        }
    }
}