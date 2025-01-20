using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace TQServer
{
    public static class NetTools
    {
        /// <summary>
        /// Makes an HTTP request to the specified URI using the specified HTTP method.
        /// </summary>
        /// <param name="uri">The target URI.</param>
        /// <param name="method">The HTTP method to use.</param>
        /// <returns>A byte array containing the response body.</returns>
        public static byte[] InvokeWebRequest(Uri uri, HttpMethod method)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(method, uri))
            {
                // Send the request and get the response
                using (HttpResponseMessage response = client.Send(request))
                {
                    return response.Content.ReadAsByteArrayAsync().Result;
                }
            }
        }

        /// <summary>
        /// Asynchronous version of InvokeWebRequest.
        /// </summary>
        /// <param name="uri">The target URI.</param>
        /// <param name="method">The HTTP method to use.</param>
        /// <returns>A task that resolves to a byte array containing the response body.</returns>
        public static async Task<byte[]> InvokeWebRequestAsync(Uri uri, HttpMethod method)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(method, uri))
            using (HttpResponseMessage response = await client.SendAsync(request))
            {
                // Read the response content as a byte array
                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        /// <summary>
        /// Helper method to convert a string URL to a Uri object, assuming "http://" if not specified.
        /// </summary>
        /// <param name="url">The URL as a string.</param>
        /// <returns>The Uri object.</returns>
        public static Uri ConvertToUri(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty.", nameof(url));

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                throw new UriFormatException("Invalid URL format.");

            return uri;
        }
    }
}
