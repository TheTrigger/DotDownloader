using Oibi.Download.Extensions;
using Oibi.Download.Models.Emuns;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Oibi.Download
{
    /// <summary>
    /// Part downloader
    /// </summary>
    internal class PartMonitor
    {
        private readonly DotDownloader _manager;
        private readonly HttpClient _httpClient;
        private readonly FileStream _fileStream;

        internal readonly PartDownloadSettings _settings;

        /// <summary>
        /// Create new downloader that download things
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="settings"></param>
        internal PartMonitor(DotDownloader manager, HttpClient httpClient, PartDownloadSettings settings)
        {
            _manager = manager;
            _httpClient = httpClient;
            _settings = settings;
            _fileStream = new FileStream(_settings.OutFile.FullName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, FileStreamExtensions.FileStreamBufferLength, FileOptions.Asynchronous);
        }

        public long BytesToDownload => _settings.OutFile.Length;

        public long OffsetFrom { get; private set; }
        public long OffsetTo { get; private set; }

        private long lastPosition;
        private long lastLength;

        public Status Status { get; private set; }

        public double Progress
        {
            get
            {
                if (_fileStream is null)
                    return default;

                return (1d * (_fileStream.CanRead ? _fileStream.Position : lastPosition) / lastLength);
            }
        }

        public FileStream GetFileStream() => _fileStream;

        public Task StartDownloadAsync(CancellationToken cancellationToken) => DownloadAsync(cancellationToken);

        internal async Task DownloadAsync(CancellationToken cancellationToken)
        {
#if DEBUG
            Console.WriteLine($"STARTED {_settings.Uri.AbsoluteUri} is {_settings.OutFile.Name}");
#endif
            lastPosition = default;
            lastLength = _fileStream.Length;

            var request = new HttpRequestMessage(HttpMethod.Get, _settings.Uri);
            if (_manager.SupportsAcceptRanges) // not AsMultiPart so we can resume dl - WTF?
            {
                Status = Status.Resuming;

                _ = await _fileStream.PositionToNonZeroOffsetAsync(); // resume (0x00 check from end)
                if (_fileStream.Position == _fileStream.Length)
                {
                    //Console.WriteLine($"SKIPPED - DOWNLOAD ALREADY COMPLETED: {_settings.Uri.AbsoluteUri}");
                    lastPosition = _fileStream.Position;
                    return;
                }

                OffsetFrom = _settings.RemoteOffset + _fileStream.Position;
                OffsetTo = _settings.RemoteOffset + _fileStream.Length;
                request.Headers.Range = new RangeHeaderValue(OffsetFrom, OffsetTo);
            }

            Status = Status.Downloading;

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var dataStream = await response.Content.ReadAsStreamAsync();

            if (response.Content.Headers.ContentLength - 1 != response.Content.Headers.ContentRange.To - response.Content.Headers.ContentRange.From)
                throw new AccessViolationException($"{nameof(HttpContentHeaders.ContentLength)} and {nameof(HttpContentHeaders.ContentRange)} does not match!");

            if (response.Content.Headers.ContentLength - 1 != _fileStream.Length - _fileStream.Position)
                throw new FileLoadException($"{nameof(HttpContentHeaders.ContentLength)} is not equal to file length!");

#if DEBUG
            await CopyStream(dataStream, _fileStream, cancellationToken);
#else
            await dataStream.CopyToAsync(_fileStream, 32768, cancellationToken);
#endif
            Status = Status.Done;
        }

#if DEBUG

        /*      Implement this to get dl speed ? */

        internal async Task CopyStream(Stream input, Stream output, CancellationToken cancellationToken)
        {
            int bytesRead;
            long bytesReadComplete = default;

            //var stopWatch = new Stopwatch();
            // TODO: new Memory<byte>
            var buffer = new byte[32768];
            while (!cancellationToken.IsCancellationRequested)
            {
                bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0)
                    break;

                bytesReadComplete += bytesRead;

                await output.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                lastPosition = output.Position;

#if DEBUG
                await output.FlushAsync(cancellationToken);
#endif

                //stopWatch.Stop();
                //if (stopWatch.ElapsedMilliseconds > 0)
                // _speed?.Report(bytesRead * 8 / stopWatch.ElapsedMilliseconds);
                //stopWatch.Restart();
            }

            await output.FlushAsync(cancellationToken);
        }

#endif
    }
}