using System.Net.Http;

namespace Oibi.Download.Extensions
{
    public static class HttpResponseMessageExtensions
    {
        public static bool SupportsAcceptRanges(this HttpResponseMessage httpResponseMessage)
        {
            // TODO: prevent null exception?
            var accepRangeValues = httpResponseMessage.Headers.AcceptRanges;
            foreach (var ar in accepRangeValues)
            {
                return ar switch
                {
                    "none" => false,
                    "bytes" => true,
                    _ => throw new HttpRequestException($"Accept-Ranges header with unknown state: `{ar}`"),
                };
            }

            return false;
        }

        public static long? ContentLength(this HttpResponseMessage httpResponseMessage)
        {
            // TODO: handle no declared content length
            return httpResponseMessage?.Content.Headers.ContentLength;
        }
    }
}