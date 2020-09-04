using System;
using System.IO;

namespace Oibi.Download
{
    /// <summary>
    /// Settings about download / split download
    /// </summary>
    internal sealed class PartDownloadSettings
    {
        /// <summary>
        /// Remote resource
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// File (part) to write
        /// </summary>
        public FileInfo OutFile { get; set; }

        /// <summary>
        /// Remote offset to resume download
        /// </summary>
        public long RemoteOffset { get; set; }
    }
}