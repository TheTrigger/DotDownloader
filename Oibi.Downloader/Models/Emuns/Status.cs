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
        Resuming,

        /// <summary>
        /// Just downloading
        /// </summary>
        Downloading,

        /// <summary>
        /// Joining files
        /// </summary>
        Joining,

        /// <summary>
        /// All is fine
        /// </summary>
        Done,
    }
}