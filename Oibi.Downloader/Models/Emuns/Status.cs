namespace Oibi.Download.Models.Emuns
{
    internal enum Status
    {
        /// <summary>
        /// Waiting to inizialize
        /// </summary>
        Idle,

        /// <summary>
        /// Search for file offset to resume download
        /// </summary>
        SearchingOffset,

        /// <summary>
        /// Just downloading
        /// </summary>
        NetworkDownloading,

        /// <summary>
        /// Done
        /// </summary>
        Done,
    }
}