using Oibi.Download.Extensions;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Oibi.Download
{
    /// <summary>
    /// Part downloader
    /// </summary>
    internal class PartMonitor // TODO: IDisposable?
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        internal readonly PartDownloadSettings _settings;

        internal PartMonitor(PartDownloadSettings settings)
        {
            _settings = settings;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public Task CurrentTask { get; private set; }

        /// <summary>
        /// Current reads till now
        /// </summary>
        //public double Progress => fileStream != null ? (1d * fileStream.Position / fileStream.Length) : 0;

        public long BytesToDownload => _settings.File.Length;

        public double Progress { get; private set; }

        public double CurrentDownloadSpeed => throw new NotImplementedException();

        public Task StartDownloadAsync()
        {
            return CurrentTask = DownloadAsync();
        }

        internal async Task DownloadAsync()
        {
#if DEBUG
            Console.WriteLine($"STARTED {_settings.Uri.AbsoluteUri} is {_settings.File.Name}");
#endif
            using var fileStream = new FileStream(_settings.File.FullName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, Extensions.Extensions.FileStreamBufferLenght, FileOptions.Asynchronous);
            using var webClient = new WebClient();

            //webClient.Proxy = new WebProxy("http://proxyserver:80/", true);
            //webClient.Proxy.Credentials = new NetworkCredential("user", "password");
            //webClient.Headers["User-Agent"] = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1)";

            if (_settings.FileDownloader.SupportsAcceptRanges) // not AsMultiPart so we can resume dl - WTF?
            {
                _ = await fileStream.PositionToNonZeroOffsetAsync(); // resume (0x00 check from end)
                if (fileStream.Position == fileStream.Length)
                {
                    //Console.WriteLine($"SKIPPED - DOWNLOAD ALREADY COMPLETED: {_settings.Uri.AbsoluteUri}");
                    return;
                }

                webClient.Headers.Add("Range", string.Format("bytes={0}-{1}", _settings.StartOffset + fileStream.Position, _settings.StartOffset + fileStream.Length));
            }

            using var webStream = await webClient.OpenReadTaskAsync(_settings.Uri);

            // TODO: improve content-length read - handle null?
            //var bytesTotal = Convert.ToInt64(webClient.ResponseHeaders["Content-Length"]);

            //if (_bytesTotal != (_settings.StartOffset + fileStream.Length) - (_settings.StartOffset + fileStream.Position))
            //throw new Exception("What the hell are you doing?");

            int bytesRead;
            long bytesReadComplete = 0;

            // TODO: new Memory<byte>
            var buffer = new byte[32768];

            //var stopWatch = new Stopwatch();

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                //stopWatch.Restart();

                bytesRead = await webStream.ReadAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token);
                if (bytesRead == 0)
                    break;

                bytesReadComplete += bytesRead;

                await fileStream.WriteAsync(buffer, 0, bytesRead, _cancellationTokenSource.Token);

                // lazy cause fileStream have to free resource, so you have to implement IDisposable
                Progress = (1d * fileStream.Position / fileStream.Length);

                //_lastChunk = DateTime.Now;
                //stopWatch.Stop();
                //if (stopWatch.ElapsedMilliseconds > 0)
                // _speed?.Report(bytesRead * 8 / stopWatch.ElapsedMilliseconds);
            }
        }

        internal async Task PauseAsync() => throw new NotImplementedException();

        internal async Task ResumeAsync() => throw new NotImplementedException();

        internal void Abort()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource.Cancel();
        }
    }
}