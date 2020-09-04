using System;
using System.Linq;

namespace Oibi.Download.Extensions
{
    public static class UriExtensions
    {
        public static Uri GetParentUri(this Uri uri)
        {
            return new Uri(uri.AbsoluteUri.Remove(uri.AbsoluteUri.Length - uri.Segments.Last().Length - uri.Query.Length).TrimEnd('/'));
        }
    }
}