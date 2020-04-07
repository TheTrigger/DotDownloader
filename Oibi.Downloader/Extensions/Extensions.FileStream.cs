using System;
using System.IO;
using System.Threading.Tasks;

namespace Oibi.Download.Extensions
{
    public static partial class Extensions
    {
        internal const int FileStreamBufferLenght = 33554432; // 33 mb

        /// <summary>
        /// Find first zero from end
        /// </summary>
        /// <param name="fileStream"></param>
        /// <returns>found position</returns>
        public static async Task<long> PositionToNonZeroOffsetAsync(this FileStream fileStream)
        {
            var buffer = new byte[FileStreamBufferLenght];
            long totalBytesRead = 0;

            while (totalBytesRead < fileStream.Length)
            {
                int bytesToRead = (int)(fileStream.Length <= FileStreamBufferLenght ? fileStream.Length : FileStreamBufferLenght);

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