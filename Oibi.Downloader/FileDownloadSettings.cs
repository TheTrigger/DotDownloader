using System;
using System.IO;

namespace Oibi.Download
{
    /// <summary>
    /// Setting for a single file download
    /// </summary>
    public sealed class FileDownloadSettings
    {
        public Uri RemoteResource { get; set; }
        public FileInfo LocalResource { get; set; }
        public int Priority { get; set; } = 0;
    }
}