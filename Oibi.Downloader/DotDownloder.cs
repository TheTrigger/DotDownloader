using Oibi.Download.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Oibi.Download
{
    public class DotDownloder
    {
        /// <summary>
        /// Suffix for multiparts download
        /// </summary>
        private const string NameMultiPartSuffix = ".part-{0}.tmp";

        private readonly Uri _uri;

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
        /// Have Accept-Ranges header?
        /// </summary>
        public bool SupportsAcceptRanges
        {
            get
            {
                if (_responseMessage is null)
                    throw new NotSupportedException($"Please call {nameof(CheckHeadAsync)} first");

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
        private long? _contentLength => _responseMessage.ContentLenght();

        /// <summary>
        /// Current download tasks
        /// </summary>
        private IList<PartMonitor> _downloadMonitors;

        /// <summary>
        /// TODO: nullable?
        /// </summary>
        public double Progress
        {
            get
            {
                if (!_contentLength.HasValue || _downloadMonitors is null)
                    return default;

                double progress = default;
                foreach (var dm in _downloadMonitors)
                    progress += dm.Progress * (1d * dm.BytesToDownload / _contentLength.Value);

                return Math.Round(progress, 4);
            }
        }

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

        public DotDownloder(FileDownloadSettings settings)
        {
            _uri = settings.RemoteResource;
            _baseFileInfo = settings.LocalResource;
        }

        /// <summary>
        /// Check resource - HEAD request
        /// </summary>
        private async Task<bool> CheckHeadAsync()
        {
            using var client = new HttpClient();
            var response = new HttpRequestMessage(HttpMethod.Head, _uri);

            _responseMessage = await client.SendAsync(response);

            return _responseMessage.IsSuccessStatusCode;
        }

        /// <summary>
        /// Check if multi-part is valid. Set <see cref="AsMultiPart"/>
        /// </summary>
        private async Task CheckMultiPartAsync()
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
                return;
            }

            if (_canResume)
                AsMultiPart = files.Length;
        }

        public async Task VerifyAsync()
        {
            await CheckHeadAsync();
            await CheckMultiPartAsync();

            _downloadMonitors = new List<PartMonitor>(AsMultiPart);

            if (_contentLength is null)
            {
                throw new NotImplementedException("What to do? no content length");
            }

            // do not split under 1mb
            if (_contentLength < 1024 * 1024)
            {
                AsMultiPart = 1;
            }

            var singleSize = (_contentLength / AsMultiPart).Value;
            var rest = (_contentLength % AsMultiPart).Value;

            if (singleSize == 0)
                throw new ArgumentOutOfRangeException($"{nameof(_contentLength)} cannot be zero");

            for (int i = 1; i <= AsMultiPart; i++)
            {
                var path = string.Format($"{_baseFileInfo.FullName}{NameMultiPartSuffix}", i);
                using var fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite); //, Extensions.Extensions.FileStreamBufferLenght, FileOptions.Asynchronous);
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

                // TODO: reuse same stream?
                await fileStream.DisposeAsync();

                var fi = new FileInfo(path);
                _fileParts.Add(fi);

                var settings = new PartDownloadSettings
                {
                    FileDownloader = this,
                    Uri = _uri,
                    File = fi,
                    StartOffset = start,
                };

                var monitor = new PartMonitor(settings);
                _downloadMonitors.Add(monitor);
            }
        }

        public async Task DownloadAsync()
        {
            await VerifyAsync().ConfigureAwait(false);

            foreach (var m in _downloadMonitors)
                _ = m.StartDownloadAsync().ConfigureAwait(false);

            Task.WaitAll(_downloadMonitors.Select(s => s.CurrentTask).ToArray());

            if (!_downloadMonitors.All(t => t.CurrentTask.IsCompletedSuccessfully))
                throw new AggregateException(_downloadMonitors.Select(s => s.CurrentTask.Exception));

            await JoinParts();
        }

        public void Abort()
        {
            foreach (var m in _downloadMonitors)
                m.Abort();
        }

        /// <summary>
        /// Join splitted downloads
        /// </summary>
        /// <returns></returns>
        private async Task JoinParts()
        {
            // TODO: if single file just rename 😉

            using var mainFileStream = new FileStream(_baseFileInfo.FullName, FileMode.CreateNew, FileAccess.Write);

            var parts = _fileParts.OrderBy(o => o.Name);
            foreach (var part in parts)
            {
#if DEBUG
                using var fileStream = new FileStream(part.FullName, FileMode.Open, FileAccess.Read, FileShare.None, Extensions.Extensions.FileStreamBufferLenght);
#else
                using var fileStream = new FileStream(part.FullName, FileMode.Open, FileAccess.Read, FileShare.None, Extensions.Extensions.FileStreamBufferLenght , FileOptions.DeleteOnClose);
#endif
                // TODO: maybe a day, a progress report
                await fileStream.CopyToAsync(mainFileStream);
            }
        }
    }
}