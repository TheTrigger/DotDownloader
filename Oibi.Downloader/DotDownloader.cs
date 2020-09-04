using Oibi.Download.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Oibi.Download
{
    public class DotDownloader
    {
        //private readonly CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Suffix for multiparts download
        /// </summary>
        private const string NameMultiPartSuffix = ".part-{0}.tmp";

        private readonly Uri _uri;
        private readonly HttpClient _httpClient;

        // httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "Your Oauth token");

        /// <summary>
        /// File to create
        /// </summary>
        private readonly FileInfo _baseFileInfo;

        /// <summary>
        /// Parts (temporary parts files)
        /// </summary>
        private readonly IList<FileInfo> _fileParts = new List<FileInfo>();

        /// <summary>
        /// Check response. <see cref="https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types"/>
        /// </summary>
        private HttpResponseMessage _responseMessage;

        /// <summary>
        /// Has Accept-Ranges header?
        /// </summary>
        public bool SupportsAcceptRanges
        {
            get
            {
                if (_responseMessage is null)
                    throw new NotSupportedException($"Please call {nameof(RequestHeadAsync)} first");

                return _responseMessage.SupportsAcceptRanges();
            }
        }

        /// <summary>
        /// Default multipart value
        /// </summary>
        private int _asMultiPart = 4;

        /// <summary>
        /// Can resume download?
        /// </summary>
        private bool _canResume = false;

        /// <summary>
        /// Expected content-length
        /// </summary>
        private long? ContentLength => _responseMessage.ContentLength();

        /// <summary>
        /// Current download tasks
        /// </summary>
        private readonly ConcurrentBag<PartMonitor> _downloadMonitors = new ConcurrentBag<PartMonitor>();

        /// <summary>
        /// TODO: nullable?
        /// </summary>
        public double Progress
        {
            get
            {
                if (!ContentLength.HasValue)
                    return default;

                double progress = default;
                progress = _downloadMonitors.Sum(s => s.Progress * (1d * s.BytesToDownload / ContentLength.Value));

                return Math.Round(progress, 4);
            }
        }

        /// <summary>
        /// Is download active
        /// </summary>
        public bool IsDownloading { get; private set; }

        /// <summary>
        /// Multipart download. Default = 1
        /// </summary>
        public int AsMultiPart
        {
            get => SupportsAcceptRanges ? _asMultiPart : 1;
            set
            {
                if (value < 1)
                    throw new OverflowException($"{nameof(AsMultiPart)} cannot be less than 1");
                if (value > 16)
                    throw new OverflowException($"{nameof(AsMultiPart)} cannot be bigger than 16");

                _asMultiPart = value;
            }
        }

        /// <summary>
        /// Instantiate a download manager
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="authentication">eg: <code>new AuthenticationHeaderValue("Bearer", "Your Oauth token")</code></param>
        public DotDownloader(FileDownloadSettings settings, string userAgent, AuthenticationHeaderValue authentication)
        {
            _uri = settings.RemoteResource;
            _baseFileInfo = settings.LocalResource;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization ??= authentication;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent ?? $"Mozilla/5.0 ({Environment.OSVersion.Platform}; {Environment.OSVersion.Version}; {Environment.OSVersion.ServicePack}) Oibi.Downloader {Environment.Version}");

            Directory.CreateDirectory(_baseFileInfo.Directory.FullName);
        }

        public DotDownloader(FileDownloadSettings settings, AuthenticationHeaderValue authentication) : this(settings, null, authentication)
        {
        }

        public DotDownloader(FileDownloadSettings settings, string userAgent) : this(settings, userAgent, null)
        {
        }

        public DotDownloader(FileDownloadSettings settings) : this(settings, null, null)
        {
        }

        /// <summary>
        /// Check resource - HEAD request
        /// </summary>
        private Task<HttpResponseMessage> RequestHeadAsync(CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Head, _uri);
            return _httpClient.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Check if multi-part is valid. Set <see cref="AsMultiPart"/>
        /// </summary>
        private IEnumerable<FileInfo> CheckResumeDiskFiles()
        {
            var files = Directory.GetFiles(_baseFileInfo.Directory.FullName, $"{_baseFileInfo.Name}{string.Format(NameMultiPartSuffix, "*")}", SearchOption.TopDirectoryOnly);
            _canResume = files.Length > 0;

            if (!_responseMessage.SupportsAcceptRanges())
            {
                // TODO: valuta cosa fare
                if (files.Length > 0)
                {
                    throw new NotSupportedException($"Accept-Ranges not supported but found multiple parts on disk ({files.Length})");
                }

                AsMultiPart = 1;
            }
            else
            {
                if (_canResume)
                {
                    AsMultiPart = files.Length;
                }
            }

            return files.Select(s => new FileInfo(s));
        }

        private async Task VerifyAsync(CancellationToken cancellationToken)
        {
            _responseMessage = await RequestHeadAsync(cancellationToken).ConfigureAwait(false);
            var files = CheckResumeDiskFiles();

            if (ContentLength is null)
                throw new NotImplementedException("Not set content length");

            if (ContentLength.Value == default)
                throw new ArgumentException(message: "Zero content length", paramName: nameof(ContentLength));

            // do not split under 1mb
            if (ContentLength < 1024 * 1024)
            {
                AsMultiPart = 1;
            }

            var singleSize = (ContentLength / AsMultiPart).Value;
            var rest = (ContentLength % AsMultiPart).Value;

            if (singleSize == default)
                throw new ArgumentOutOfRangeException(message: "Content length cannot be zero", paramName: nameof(ContentLength));

            _downloadMonitors.Clear();
            for (int i = 1; i <= AsMultiPart; i++)
            {
                var path = string.Format($"{_baseFileInfo.FullName}{NameMultiPartSuffix}", i);
                using var fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite); //, Extensions.Extensions.FileStreamBufferLength, FileOptions.Asynchronous);
                long start = default;

                if (i == 1)
                {
                    start = 0;
                    fileStream.SetLength(singleSize + rest - 1);
                }
                else
                {
                    start = _fileParts.Sum(f => f.Length + 1);
                    fileStream.SetLength(singleSize - 1);
                }

                await fileStream.DisposeAsync();

                var file = new FileInfo(path);
                _fileParts.Add(file);

                var settings = new PartDownloadSettings
                {
                    Uri = _uri,
                    OutFile = file,
                    RemoteOffset = start,
                };

                var monitor = new PartMonitor(this, _httpClient, settings);

                _downloadMonitors.Add(monitor);
            }
        }

        /// <summary>
        /// Start download processes
        /// </summary>
        public async Task DownloadAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (IsDownloading)
                    throw new Exception("Download already started");

                IsDownloading = true;
                await VerifyAsync(cancellationToken).ConfigureAwait(false);
                var tasks = _downloadMonitors.Select(s => s.StartDownloadAsync(cancellationToken));
                await Task.WhenAll(tasks);
                await ConcatFilesAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                IsDownloading = false;
            }
        }

        /// <summary>
        /// Start download process
        /// </summary>
        public Task DownloadAsync() => DownloadAsync(CancellationToken.None);

        /// <summary>
        /// Join splitted downloads
        /// </summary>
        /// <returns></returns>
        private async Task ConcatFilesAsync(CancellationToken cancellationToken)
        {
            // TODO: if single file just rename 😉

            using var outStream = new FileStream(_baseFileInfo.FullName, FileMode.CreateNew, FileAccess.Write);

            foreach (var dm in _downloadMonitors)
            {
                using var stream = dm.GetFileStream();
                stream.Position = default;

                outStream.Position = dm._settings.RemoteOffset;
                await stream.CopyToAsync(outStream, cancellationToken);

                await stream.FlushAsync(cancellationToken);
                stream.Close();
                await stream.DisposeAsync();

#if !DEBUG
                File.Delete(stream.Name);
#endif
            }

            /*
            var parts = _fileParts.OrderBy(o => o.Name);
            foreach (var part in parts)
            {
#if DEBUG
                using var fileStream = new FileStream(part.FullName, FileMode.Open, FileAccess.Read, FileShare.None, FileStreamExtensions.FileStreamBufferLength);
#else
                using var fileStream = new FileStream(part.FullName, FileMode.Open, FileAccess.Read, FileShare.None, FileStreamExtensions.FileStreamBufferLength , FileOptions.DeleteOnClose);
#endif
                // TODO: maybe a day, a progress report
                await fileStream.CopyToAsync(mainFileStream, cancellationToken);
            }
            */
        }
    }
}