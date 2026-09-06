using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

// Exercises the exact candidate assembly. No receiver/form construction,
// installed settings, real GitHub requests, executables or phone data.
internal static class UpdateTransportHarness
{
    private const BindingFlags All = BindingFlags.Static | BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
    private const string Json = "{\"tag_name\":\"v9.8.7\",\"name\":\"Test release\",\"body\":\"Local test\",\"assets\":[]}";
    private static Assembly assembly;
    private static MethodInfo download;
    private static int passed;

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static object Call(MethodInfo method, object target, params object[] args)
    {
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        return Call(target.GetType().GetMethod(name, All), target, args);
    }

    private static Type ProductType(string name)
    {
        return assembly.GetType("AirPlayReceiverMvp." + name, true);
    }

    private static void Run(string name, Action action)
    {
        action();
        passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static string Fetch(Server server, CancellationToken token,
        int timeout = 3000, int limit = 1024 * 1024)
    {
        var request = (HttpWebRequest)WebRequest.Create(server.Uri);
        request.Proxy = null; // Test traffic must stay on loopback, even with a system proxy.
        server.ClientRequest = request;
        string result = (string)Call(download, null, request, token, limit, timeout);
        Assert(!request.AllowAutoRedirect && request.AutomaticDecompression ==
            DecompressionMethods.None, "Metadata redirect/decompression policy changed.");
        return result;
    }

    private static void Success(string body, bool knownLength)
    {
        using (var server = new Server(delegate(Server s, NetworkStream stream)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            s.Header(stream, knownLength ? bytes.Length : -1);
            stream.Write(bytes, 0, bytes.Length);
        }))
        {
            Assert(Fetch(server, CancellationToken.None) == body.TrimStart('\uFEFF'),
                "UTF-8 body was not preserved.");
            object release = Call(ProductType("UpdateService").GetMethod(
                "ParseLatestRelease", All), null, body.TrimStart('\uFEFF'), new Version(1, 0, 0));
            Assert(new Version(9, 8, 7).Equals(release.GetType().GetField(
                "Version", All).GetValue(release)), "Transport result did not parse as a release.");
            Assert(server.Request.Contains("Accept: application/vnd.github+json") &&
                server.Request.Contains("User-Agent: AeroMirror-Windows/") &&
                server.Request.Contains("X-GitHub-Api-Version: 2026-03-10"),
                "Required GitHub metadata headers are missing.");
        }
    }

    private static void SlowResponse(bool headers, bool progress)
    {
        using (var server = new Server(delegate(Server s, NetworkStream stream)
        {
            if (headers)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(Json);
                s.Header(stream, bytes.Length);
                if (progress)
                {
                    foreach (byte item in bytes)
                    {
                        stream.WriteByte(item);
                        Interlocked.Increment(ref s.Writes);
                        if (s.Stop.WaitOne(35)) return;
                    }
                    return;
                }
                stream.WriteByte(bytes[0]);
            }
            s.Stop.WaitOne(4000);
        }))
        {
            var watch = Stopwatch.StartNew();
            Throws<TimeoutException>(delegate { Fetch(server, CancellationToken.None, 350); });
            Assert(watch.ElapsedMilliseconds < 2500, "Whole-response timeout was not bounded.");
            if (progress)
                Assert(server.Writes >= 3, "The slow response did not exercise progress across reads.");
        }
    }

    private static object Scope()
    {
        return Activator.CreateInstance(ProductType("UpdateWorkScope"), true);
    }

    private static CancellationToken Token(object operation)
    {
        return (CancellationToken)operation.GetType().GetField("Token", All).GetValue(operation);
    }

    private static void CancelDuringRead(bool headers, bool close)
    {
        using (var server = new Server(delegate(Server s, NetworkStream stream)
        {
            if (headers)
            {
                s.Header(stream, Encoding.UTF8.GetByteCount(Json));
                stream.WriteByte((byte)'{');
            }
            s.Sending.Set();
            s.Stop.WaitOne(5000);
        }))
        {
            object scope = Scope();
            object operation = Invoke(scope, "Begin");
            CancellationToken token = Token(operation);
            Exception error = null;
            var thread = new Thread(delegate()
            {
                try { Fetch(server, token); }
                catch (Exception ex) { error = ex; }
                finally { ((IDisposable)operation).Dispose(); }
            });
            thread.IsBackground = true;
            thread.Start();
            Assert(server.Sending.WaitOne(2500), "Test server was not reached.");
            if (headers)
                Assert(SpinWait.SpinUntil(delegate
                {
                    return server.ClientRequest != null && server.ClientRequest.HaveResponse;
                }, 1500), "Cancellation did not reach the response-body phase.");
            if (close) ((IDisposable)scope).Dispose();
            else Invoke(scope, "CancelPending");
            Assert(thread.Join(2500), "Cancellation did not release the network worker.");
            var cancelled = error as OperationCanceledException;
            Assert(cancelled != null && cancelled.CancellationToken == token,
                "Owner cancellation was misreported as a network failure.");
            if (!close)
            {
                object next = Invoke(scope, "Begin");
                Assert(next != null && !Token(next).IsCancellationRequested,
                    "Canceled work poisoned the next enabled operation.");
                ((IDisposable)next).Dispose();
            }
            ((IDisposable)scope).Dispose();
            Assert(Invoke(scope, "Begin") == null, "Disposed owner accepted new work.");
        }
    }

    private static void ScopeRaces()
    {
        for (int i = 0; i < 200; i++)
        {
            object scope = Scope();
            object operation = Invoke(scope, "Begin");
            Exception failure = null;
            using (var start = new ManualResetEvent(false))
            {
                var worker = new Thread(delegate()
                {
                    start.WaitOne();
                    try { ((IDisposable)operation).Dispose(); }
                    catch (Exception ex) { failure = ex; }
                });
                worker.IsBackground = true;
                worker.Start();
                start.Set();
                Invoke(scope, "CancelPending");
                ((IDisposable)scope).Dispose();
                Assert(worker.Join(2000), "Cancellation/completion deadlocked.");
                Assert(failure == null, "Cancellation raced source disposal: " + failure);
                ((IDisposable)operation).Dispose();
            }
        }
    }

    private static void CancelDownload(string testRoot)
    {
        // Redirect persistence before exercising the production handoff; never
        // load settings or operate on the user's staged installer directory.
        Call(ProductType("AppSettings").GetMethod("SetStorageRootForTests", All),
            null, testRoot);
        Type service = ProductType("UpdateService");
        Type infoType = ProductType("UpdateInfo");
        object info = Activator.CreateInstance(infoType, true);
        infoType.GetField("Version", All).SetValue(info, new Version(9, 8, 7));
        infoType.GetField("InstallerName", All).SetValue(info, "AeroMirror-Setup-9.8.7.exe");
        infoType.GetField("InstallerUrl", All).SetValue(info,
            "https://github.com/Nadejny/aeromirror/releases/download/v9.8.7/AeroMirror-Setup-9.8.7.exe");
        infoType.GetField("InstallerSha256", All).SetValue(info, new string('a', 64));
        MethodInfo verify = service.GetMethod("DownloadAndVerify", All, null,
            new Type[] { infoType, typeof(Action<Uri, string>), typeof(CancellationToken) }, null);
        string path = null;
        using (var cancel = new CancellationTokenSource())
        {
            Action<Uri, string> simulatedDownload = delegate(Uri uri, string destination)
            {
                path = destination;
                File.WriteAllText(destination, "inert canceled download fixture");
                cancel.Cancel();
            };
            Throws<OperationCanceledException>(delegate
            {
                Call(verify, null, info, simulatedDownload, cancel.Token);
            });
        }
        Assert(path != null && !File.Exists(path), "Canceled download remained staged.");
    }

    private static int Main(string[] args)
    {
        try
        {
            assembly = Assembly.LoadFrom(args[0]);
            download = ProductType("ReleaseMetadataClient").GetMethod("Download", All,
                null, new Type[] { typeof(HttpWebRequest), typeof(CancellationToken),
                    typeof(int), typeof(int) }, null);
            Assert(download != null, "Candidate lacks the production bounded metadata reader.");
            Run("UTF-8 metadata and exact headers", delegate { Success(Json, true); });
            Run("UTF-8 BOM and unknown body length", delegate { Success("\uFEFF" + Json, false); });
            Run("delayed response headers", delegate { SlowResponse(false, false); });
            Run("stalled response body", delegate { SlowResponse(true, false); });
            Run("whole-transfer budget despite progressing reads", delegate { SlowResponse(true, true); });
            Run("owner closes during response headers", delegate { CancelDuringRead(false, true); });
            Run("owner closes during response body", delegate { CancelDuringRead(true, true); });
            Run("opt-out cancels I/O; opt-in starts a fresh operation", delegate { CancelDuringRead(true, false); });
            Run("already canceled request performs no I/O", delegate
            {
                using (var server = new Server(delegate(Server s, NetworkStream stream) { }))
                using (var cancel = new CancellationTokenSource())
                {
                    cancel.Cancel();
                    Throws<OperationCanceledException>(delegate { Fetch(server, cancel.Token); });
                    Assert(!server.Sending.WaitOne(0) && server.Request == null,
                        "Pre-canceled metadata made a request.");
                }
            });
            Run("body at the exact configured byte limit", delegate
            {
                using (var server = new Server(delegate(Server s, NetworkStream stream)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(Json);
                    s.Header(stream, bytes.Length);
                    stream.Write(bytes, 0, bytes.Length);
                }))
                    Assert(Fetch(server, CancellationToken.None, 3000,
                        Encoding.UTF8.GetByteCount(Json)) == Json, "Exact byte limit was rejected.");
            });
            Run("body budget with declared and streamed lengths", delegate
            {
                foreach (bool declared in new bool[] { true, false })
                    using (var server = new Server(delegate(Server s, NetworkStream stream)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(Json);
                        s.Header(stream, declared ? bytes.Length : -1);
                        stream.Write(bytes, 0, bytes.Length);
                    }))
                        Throws<InvalidDataException>(delegate
                        {
                            Fetch(server, CancellationToken.None, 3000, 64);
                        });
            });
            Run("redirect is not followed", delegate
            {
                using (var server = new Server(delegate(Server s, NetworkStream stream)
                {
                    s.Write(stream, "HTTP/1.1 302 Found\r\nLocation: " + s.Uri +
                        "other\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                }))
                    Throws<InvalidDataException>(delegate { Fetch(server, CancellationToken.None); });
            });
            Run("HTTP failure cleanup and next successful request", delegate
            {
                using (var server = new Server(delegate(Server s, NetworkStream stream)
                {
                    s.Write(stream, "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                }))
                    Throws<WebException>(delegate { Fetch(server, CancellationToken.None); });
                Success(Json, true);
            });
            Run("200 owner-cancel / worker-complete races", ScopeRaces);
            Run("canceled installer handoff removes its file", delegate { CancelDownload(args[1]); });
            // Explicit opt-in only: one anonymous metadata GET, no download,
            // persistence, receiver construction, Setup launch or publication.
            if (args.Length > 2 && args[2] == "--check-github")
                Run("live fixed GitHub metadata endpoint", delegate
                {
                    MethodInfo liveDownload = ProductType("ReleaseMetadataClient").GetMethod(
                        "Download", All, null,
                        new Type[] { typeof(Uri), typeof(CancellationToken) }, null);
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    string json = (string)Call(liveDownload, null,
                        new Uri((string)ProductType("UpdateService").GetField(
                            "ReleaseMetadataUrl", All).GetRawConstantValue()),
                        CancellationToken.None);
                    object release = Call(ProductType("UpdateService").GetMethod(
                        "ParseLatestRelease", All), null, json, assembly.GetName().Version);
                    Console.WriteLine("Public release parsed: " + release.GetType().GetField(
                        "Version", All).GetValue(release));
                });
            Console.WriteLine("PASS: " + passed + " update transport/lifetime scenarios on " +
                assembly.GetName().Version);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private sealed class Server : IDisposable
    {
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly Thread thread;
        private TcpClient client;
        internal readonly ManualResetEvent Stop = new ManualResetEvent(false);
        internal readonly ManualResetEvent Sending = new ManualResetEvent(false);
        internal readonly Uri Uri;
        internal volatile HttpWebRequest ClientRequest;
        internal string Request;
        internal int Writes;
        private Exception failure;

        internal Server(Action<Server, NetworkStream> serve)
        {
            listener.Start();
            Uri = new Uri("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/");
            thread = new Thread(delegate()
            {
                try
                {
                    using (client = listener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream())
                    {
                        stream.ReadTimeout = 3000;
                        stream.WriteTimeout = 3000;
                        var header = new StringBuilder();
                        while (header.Length < 16384)
                        {
                            int next = stream.ReadByte();
                            if (next < 0) return;
                            header.Append((char)next);
                            if (header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) break;
                        }
                        Request = header.ToString();
                        serve(this, stream);
                    }
                }
                // Timeouts/cancellation deliberately close this test connection.
                catch (IOException) { }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
                catch (Exception ex) { failure = ex; }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        internal void Write(NetworkStream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        internal void Header(NetworkStream stream, int length)
        {
            Write(stream, "HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\n" +
                (length >= 0 ? "Content-Length: " + length + "\r\n" : "") +
                "Connection: close\r\n\r\n");
        }

        public void Dispose()
        {
            Stop.Set();
            listener.Stop();
            if (client != null) client.Close();
            Assert(thread.Join(4000), "Loopback server did not terminate.");
            Stop.Dispose();
            Sending.Dispose();
            if (failure != null) throw new Exception("Loopback server failed.", failure);
        }
    }
}
