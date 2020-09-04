using System;
using System.IO;
using System.Threading.Tasks;

namespace Oibi.Download.Extensions
{
    public static class FileStreamExtensions
    {
        /// <summary>
        /// 33MB is good enough
        /// </summary>
        internal const int FileStreamBufferLength = 33554432;

        /// <summary>
        /// Resume position findinf the first zero from the end
        /// </summary>
        /// <param name="fileStream"></param>
        /// <returns>position offset</returns>
        public static async Task<long> PositionToNonZeroOffsetAsync(this FileStream fileStream)
        {
            var buffer = new byte[FileStreamBufferLength];
            long totalBytesRead = 0;

            while (totalBytesRead < fileStream.Length)
            {
                int bytesToRead = (int)(fileStream.Length <= FileStreamBufferLength ? fileStream.Length : FileStreamBufferLength);

                // TODO: ottimizza -> sovrapposizione lettura verso l'ultimo blocco ... Math.Max==lazy
                fileStream.Position = Math.Max(default, fileStream.Length - totalBytesRead - bytesToRead);

                var bytesRead = await fileStream.ReadAsync(buffer, 0, bytesToRead).ConfigureAwait(false);
                for (long i = bytesRead; i > 0; i--)
                {
                    if (buffer[i - 1] != 0x00)
                    {
                        return fileStream.Position -= (bytesRead - i);
                    }
                }

                totalBytesRead += bytesRead;
            }

            return fileStream.Position = 0;
        }
    }
}