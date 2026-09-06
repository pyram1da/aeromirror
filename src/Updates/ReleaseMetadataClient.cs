using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace AirPlayReceiverMvp
{
    // Metadata is small. Bound the entire transfer, not just an individual
    // Read: a progressing response must not keep an update worker alive forever.
    internal static class ReleaseMetadataClient
    {
        internal const int MaximumResponseBytes = 1024 * 1024;
        internal const int TransferTimeoutMilliseconds = 30000;

        internal static string Download(Uri uri, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var request = (HttpWebRequest)WebRequest.Create(uri);
            return Download(request, cancellation,
                MaximumResponseBytes, TransferTimeoutMilliseconds);
        }

        // The request seam lets an isolated loopback server exercise the real
        // .NET Framework transport. Production supplies only the fixed API URI.
        internal static string Download(HttpWebRequest request,
            CancellationToken cancellation, int maximumBytes, int timeoutMilliseconds)
        {
            if (request == null)
                throw new ArgumentNullException("request");
            if (maximumBytes <= 0 || maximumBytes > MaximumResponseBytes)
                throw new ArgumentOutOfRangeException("maximumBytes");
            if (timeoutMilliseconds <= 0 ||
                timeoutMilliseconds > TransferTimeoutMilliseconds)
                throw new ArgumentOutOfRangeException("timeoutMilliseconds");

            cancellation.ThrowIfCancellationRequested();
            request.Method = "GET";
            request.AllowAutoRedirect = false;
            request.AutomaticDecompression = DecompressionMethods.None;
            request.Timeout = timeoutMilliseconds;
            request.ReadWriteTimeout = timeoutMilliseconds;
            request.UserAgent = "AeroMirror-Windows/" + AppVersion.Display;
            request.Accept = "application/vnd.github+json";
            request.Headers["X-GitHub-Api-Version"] = "2026-03-10";

            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation))
            using (deadline.Token.Register(request.Abort, false))
            {
                deadline.CancelAfter(timeoutMilliseconds);
                try
                {
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                            throw new InvalidDataException(
                                "GitHub вернул неожиданный ответ при проверке обновлений.");
                        if (response.ContentLength > maximumBytes)
                            throw ResponseTooLarge();
                        using (Stream input = response.GetResponseStream())
                        using (var output = new MemoryStream())
                        {
                            if (input == null)
                                throw new InvalidDataException(
                                    "GitHub не вернул описание обновления.");
                            byte[] buffer = new byte[8192];
                            while (true)
                            {
                                ThrowIfCancelled(cancellation, deadline, null);
                                int read = input.Read(buffer, 0, buffer.Length);
                                if (read == 0)
                                    break;
                                if (output.Length + read > maximumBytes)
                                    throw ResponseTooLarge();
                                output.Write(buffer, 0, read);
                            }
                            ThrowIfCancelled(cancellation, deadline, null);
                            string json = new UTF8Encoding(false, true).GetString(
                                output.GetBuffer(), 0, (int)output.Length);
                            return json.Length > 0 && json[0] == '\uFEFF'
                                ? json.Substring(1) : json;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // GetResponse throws before entering its using block for
                    // HTTP error status codes. It still owns a response then.
                    var webError = ex as WebException;
                    if (webError != null && webError.Response != null)
                        webError.Response.Close();
                    ThrowIfCancelled(cancellation, deadline, ex);
                    if (webError != null &&
                        webError.Status == WebExceptionStatus.Timeout)
                        throw Timeout(ex);
                    throw;
                }
            }
        }

        private static void ThrowIfCancelled(CancellationToken cancellation,
            CancellationTokenSource deadline, Exception cause)
        {
            cancellation.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested)
                throw Timeout(cause);
        }

        private static TimeoutException Timeout(Exception cause)
        {
            return new TimeoutException(
                "GitHub не ответил вовремя. Повторите проверку обновлений позже.",
                cause);
        }

        private static InvalidDataException ResponseTooLarge()
        {
            return new InvalidDataException(
                "Описание обновления от GitHub превышает допустимый размер.");
        }
    }
}
