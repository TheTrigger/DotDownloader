using System;
using System.IO;

namespace Oibi.Download
{
    internal sealed class PartDownloadSettings
    {
        /// <summary>
        /// TODO: remove?
        /// </summary>
        public DotDownloder FileDownloader { get; set; }

        /// <summary>
        /// Remote resource
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// File (part) to write
        /// </summary>
        public FileInfo File { get; set; }

        /// <summary>
        /// Remote offet to require/resume download
        /// </summary>
        public long StartOffset { get; set; }

        /// <summary>
        /// Download progress
        /// </summary>
        public Progress<float> Progress { get; set; }
    }
}