using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace AirPlayReceiverMvp
{
    internal static class UpdateService
    {
        private const int MaximumInstallerBytes = 64 * 1024 * 1024;
        private const int MaximumDownloadRedirects = 5;
        private const int DownloadTimeoutMilliseconds = 30000;
        private const string ExpectedRepository = "Nadejny/aeromirror";
        private const string CanonicalRepository = "pyram1da/aeromirror";
        // GitHub's immutable repository ID survives the confirmed owner rename.
        // Keep the historical local configuration marker for installed clients.
        internal const string ReleaseMetadataUrl =
            "https://api.github.com/repositories/1324108899/releases/latest";

        internal static string RepositoryFilePath
        {
            get
            {
                return Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "update-repository.txt");
            }
        }

        internal static UpdateInfo Check()
        {
            return Check(CancellationToken.None);
        }

        internal static UpdateInfo Check(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            string repository = ReadRepository();
            if (repository.Length == 0)
                throw new InvalidOperationException(
                    "Канал обновлений ещё не настроен. " +
                    "Он станет доступен после публикации проекта на GitHub.");

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            string json = ReleaseMetadataClient.Download(
                new Uri(ReleaseMetadataUrl), cancellation);
            cancellation.ThrowIfCancellationRequested();
            UpdateInfo info = ParseLatestRelease(json, AppVersion.Current);
            cancellation.ThrowIfCancellationRequested();
            return info;
        }

        internal static UpdateInfo ParseLatestRelease(
            string json, Version currentVersion)
        {
            if (currentVersion == null)
                throw new ArgumentNullException("currentVersion");

            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(json)
                as Dictionary<string, object>;
            if (root == null)
                throw new InvalidDataException(
                    "GitHub вернул неизвестный формат ответа.");

            string tag = GetString(root, "tag_name");
            Version latest;
            if (!TryParseVersion(tag, out latest))
                throw new InvalidDataException(
                    "В последнем GitHub Release не найдена корректная версия.");

            var info = new UpdateInfo();
            info.Version = latest;
            info.VersionText = tag;
            info.Title = GetString(root, "name");
            info.Notes = CleanReleaseNotes(GetString(root, "body"));
            info.ReleasePage = GetString(root, "html_url");
            info.IsNewer = latest.CompareTo(
                currentVersion) > 0;

            object assetsValue;
            object[] assets = root.TryGetValue("assets", out assetsValue)
                ? assetsValue as object[] : null;
            if (assets != null)
            {
                string expectedInstaller =
                    "AeroMirror-Setup-" + latest.ToString(3) + ".exe";
                foreach (object assetValue in assets)
                {
                    var asset = assetValue as Dictionary<string, object>;
                    if (asset == null)
                        continue;
                    string name = GetString(asset, "name");
                    if (string.Equals(
                            name,
                            expectedInstaller,
                            StringComparison.Ordinal))
                    {
                        info.InstallerName = name;
                        info.InstallerUrl =
                            GetString(asset, "browser_download_url");
                        string digest = GetString(asset, "digest");
                        if (digest.StartsWith(
                            "sha256:", StringComparison.OrdinalIgnoreCase))
                            info.InstallerSha256 = digest.Substring(7);
                        break;
                    }
                }
            }
            return info;
        }

        internal static string DownloadAndVerify(UpdateInfo info)
        {
            return DownloadAndVerify(info, CancellationToken.None);
        }

        internal static string DownloadAndVerify(
            UpdateInfo info, CancellationToken cancellation)
        {
            return DownloadAndVerify(info, delegate(Uri uri, string path)
            {
                DownloadInstallerWithValidatedRedirects(uri, path, cancellation);
            }, cancellation);
        }

        internal static string DownloadAndVerify(
            UpdateInfo info, Action<Uri, string> download)
        {
            return DownloadAndVerify(info, download, CancellationToken.None);
        }

        internal static string DownloadAndVerify(
            UpdateInfo info, Action<Uri, string> download,
            CancellationToken cancellation)
        {
            if (download == null)
                throw new ArgumentNullException("download");
            cancellation.ThrowIfCancellationRequested();
            Uri uri;
            string name;
            ValidateDownloadCandidate(info, out uri, out name);
            string path = AutomaticUpdateService.CreateDownloadPath(name);
            bool complete = false;
            try
            {
                download(uri, path);
                cancellation.ThrowIfCancellationRequested();

                if (!File.Exists(path))
                    throw new InvalidDataException(
                        "Загрузка не создала файл установщика.");
                FileAttributes attributes = File.GetAttributes(path);
                if (!IsAcceptableDownloadedInstallerFile(
                        attributes, new FileInfo(path).Length))
                {
                    throw new InvalidDataException(
                        "Загруженный установщик не является допустимым файлом.");
                }

                string actual = ComputeSha256(path);
                if (!string.Equals(
                    actual, info.InstallerSha256,
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "SHA-256 загруженного установщика не совпал с GitHub Release.");
                cancellation.ThrowIfCancellationRequested();
                complete = true;
                return path;
            }
            catch
            {
                cancellation.ThrowIfCancellationRequested();
                throw;
            }
            finally
            {
                if (!complete)
                {
                    try { File.Delete(path); }
                    catch { }
                }
            }
        }

        private static bool IsAcceptableDownloadedInstallerFile(
            FileAttributes attributes, long length)
        {
            return length >= 0 && length <= MaximumInstallerBytes &&
                (attributes & FileAttributes.ReparsePoint) == 0 &&
                (attributes & FileAttributes.Directory) == 0;
        }

        internal static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(
                    sha.ComputeHash(stream)).Replace("-", "");
        }

        private static void ValidateDownloadCandidate(
            UpdateInfo info, out Uri uri, out string expectedName)
        {
            if (info == null || info.Version == null ||
                string.IsNullOrWhiteSpace(info.InstallerUrl))
            {
                throw new InvalidOperationException(
                    "В GitHub Release нет установщика обновления.");
            }
            if (info.Version.CompareTo(AppVersion.Current) <= 0)
            {
                throw new InvalidOperationException(
                    "Установщик не новее текущей версии AeroMirror.");
            }
            if (!IsSha256(info.InstallerSha256))
            {
                throw new InvalidOperationException(
                    "GitHub Release не содержит корректный SHA-256 установщика. " +
                    "Автоматическое обновление остановлено для безопасности.");
            }

            expectedName = "AeroMirror-Setup-" +
                info.Version.ToString(3) + ".exe";
            if (!string.Equals(
                    info.InstallerName,
                    expectedName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Имя установщика не совпадает с версией GitHub Release.");
            }

            if (!Uri.TryCreate(info.InstallerUrl, UriKind.Absolute, out uri) ||
                !string.Equals(
                    uri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Установщик обновления должен загружаться по HTTPS.");
            }
            string releasePath = "/releases/download/v" +
                info.Version.ToString(3) + "/" + expectedName;
            string actualPath = Uri.UnescapeDataString(uri.AbsolutePath);
            bool expectedPath = string.Equals(actualPath,
                    "/" + CanonicalRepository + releasePath,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actualPath,
                    "/" + ExpectedRepository + releasePath,
                    StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(
                    uri.Host, "github.com",
                    StringComparison.OrdinalIgnoreCase) ||
                !uri.IsDefaultPort ||
                uri.UserInfo.Length != 0 ||
                uri.Query.Length != 0 ||
                uri.Fragment.Length != 0 ||
                !expectedPath)
            {
                throw new InvalidOperationException(
                    "Адрес установщика не привязан к ожидаемому GitHub Release.");
            }
            string urlName = Uri.UnescapeDataString(
                Path.GetFileName(uri.AbsolutePath));
            if (!string.Equals(
                    urlName, expectedName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Адрес установщика не содержит ожидаемое точное имя файла.");
            }
        }

        private static void DownloadInstallerWithValidatedRedirects(
            Uri initialUri, string destinationPath, CancellationToken cancellation)
        {
            Uri current = initialUri;
            for (int redirect = 0;
                redirect <= MaximumDownloadRedirects;
                redirect++)
            {
                cancellation.ThrowIfCancellationRequested();
                ValidateDownloadHop(current, redirect == 0);
                var request = (HttpWebRequest)WebRequest.Create(current);
                request.Method = "GET";
                request.AllowAutoRedirect = false;
                request.Timeout = DownloadTimeoutMilliseconds;
                request.ReadWriteTimeout = DownloadTimeoutMilliseconds;
                request.UserAgent = "AeroMirror-Windows/" +
                    AppVersion.Display;
                request.Accept = "application/octet-stream";

                using (cancellation.Register(request.Abort, false))
                using (var response = GetInstallerResponse(request))
                {
                    int status = (int)response.StatusCode;
                    if (status == 301 || status == 302 || status == 303 ||
                        status == 307 || status == 308)
                    {
                        if (redirect >= MaximumDownloadRedirects)
                            throw new InvalidDataException(
                                "Слишком много перенаправлений при загрузке обновления.");
                        string location = response.Headers["Location"];
                        Uri next;
                        if (string.IsNullOrWhiteSpace(location) ||
                            !Uri.TryCreate(
                                current, location, out next))
                        {
                            throw new InvalidDataException(
                                "GitHub вернул некорректное перенаправление.");
                        }
                        current = next;
                        continue;
                    }
                    if (status != 200)
                    {
                        throw new WebException(
                            "GitHub вернул HTTP " + status + ".");
                    }
                    if (response.ContentLength > MaximumInstallerBytes)
                    {
                        throw new InvalidDataException(
                            "Установщик обновления превышает допустимый размер.");
                    }

                    using (Stream input = response.GetResponseStream())
                    using (var output = new FileStream(
                        destinationPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        FileOptions.WriteThrough))
                    {
                        if (input == null)
                            throw new InvalidDataException(
                                "GitHub не вернул данные установщика.");
                        byte[] buffer = new byte[81920];
                        long total = 0;
                        while (true)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            int read = input.Read(buffer, 0, buffer.Length);
                            if (read <= 0)
                                break;
                            total += read;
                            if (total > MaximumInstallerBytes)
                            {
                                throw new InvalidDataException(
                                    "Установщик обновления превышает допустимый размер.");
                            }
                            output.Write(buffer, 0, read);
                        }
                        output.Flush(true);
                    }
                    return;
                }
            }
            throw new InvalidDataException(
                "Не удалось завершить загрузку после перенаправлений.");
        }

        private static HttpWebResponse GetInstallerResponse(HttpWebRequest request)
        {
            try { return (HttpWebResponse)request.GetResponse(); }
            catch (WebException ex)
            {
                if (ex.Response != null)
                    ex.Response.Close();
                throw;
            }
        }

        private static void ValidateDownloadHop(Uri uri, bool initial)
        {
            if (uri == null || !string.Equals(
                    uri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase) ||
                !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                uri.Fragment.Length != 0)
            {
                throw new InvalidDataException(
                    "Каждый адрес загрузки обновления должен использовать HTTPS.");
            }
            string host = uri.Host;
            bool allowed = string.Equals(
                    host, "github.com",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    host, "release-assets.githubusercontent.com",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    host, "objects.githubusercontent.com",
                    StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(
                    ".githubusercontent.com",
                    StringComparison.OrdinalIgnoreCase) ||
                (host.StartsWith(
                        "github-production-release-asset-",
                        StringComparison.OrdinalIgnoreCase) &&
                    host.EndsWith(
                        ".s3.amazonaws.com",
                        StringComparison.OrdinalIgnoreCase));
            if (!allowed || (initial && !string.Equals(
                    host, "github.com",
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(
                    "Перенаправление загрузки ведёт за пределы GitHub CDN.");
            }
        }

        internal static bool IsSha256(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(
                value.Trim(), @"^[0-9A-Fa-f]{64}$",
                RegexOptions.CultureInvariant);
        }

        private static string ReadRepository()
        {
            if (!File.Exists(RepositoryFilePath))
                return "";
            foreach (string raw in File.ReadAllLines(
                RepositoryFilePath, Encoding.UTF8))
            {
                string value = raw.Trim();
                if (value.Length == 0 || value.StartsWith("#"))
                    continue;
                if (string.Equals(
                        value, ExpectedRepository,
                        StringComparison.Ordinal))
                    return ExpectedRepository;
            }
            return "";
        }

        private static string GetString(
            Dictionary<string, object> values, string key)
        {
            object value;
            return values.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value) : "";
        }

        private static bool TryParseVersion(string text, out Version version)
        {
            string value = (text ?? "").Trim();
            Match match = Regex.Match(
                value,
                @"^v(\d+)\.(\d+)\.(\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                version = null;
                return false;
            }
            return Version.TryParse(
                match.Groups[1].Value + "." +
                match.Groups[2].Value + "." +
                match.Groups[3].Value,
                out version);
        }

        private static string CleanReleaseNotes(string markdown)
        {
            string text = (markdown ?? "").Replace("\r\n", "\n");
            text = text.Replace("### ", "").Replace("## ", "")
                .Replace("# ", "").Replace("**", "").Replace("`", "");
            return text.Replace("\n", Environment.NewLine).Trim();
        }
    }
}
