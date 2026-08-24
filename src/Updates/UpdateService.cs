using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AirPlayReceiverMvp
{
    internal static class UpdateService
    {
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
            string repository = ReadRepository();
            if (repository.Length == 0)
                throw new InvalidOperationException(
                    "Канал обновлений ещё не настроен. " +
                    "Он станет доступен после публикации проекта на GitHub.");

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            string api = "https://api.github.com/repos/" +
                repository + "/releases/latest";
            string json;
            using (var client = CreateClient())
                json = client.DownloadString(api);

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
                AppVersion.Current) > 0;

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
                            StringComparison.OrdinalIgnoreCase))
                    {
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
            if (info == null || string.IsNullOrWhiteSpace(info.InstallerUrl))
                throw new InvalidOperationException(
                    "В GitHub Release нет установщика обновления.");
            if (string.IsNullOrWhiteSpace(info.InstallerSha256))
                throw new InvalidOperationException(
                    "GitHub Release не содержит SHA-256 установщика. " +
                    "Автоматическое обновление остановлено для безопасности.");

            var uri = new Uri(info.InstallerUrl);
            if (!string.Equals(
                uri.Scheme, Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Установщик обновления должен загружаться по HTTPS.");

            string name = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(name) ||
                !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = "AeroMirror-Update.exe";
            string path = Path.Combine(
                Path.GetTempPath(),
                "AeroMirror-" + Guid.NewGuid().ToString("N") + "-" + name);
            bool complete = false;
            try
            {
                using (var client = CreateClient())
                    client.DownloadFile(uri, path);

                string actual;
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                    actual = BitConverter.ToString(
                        sha.ComputeHash(stream)).Replace("-", "");
                if (!string.Equals(
                    actual, info.InstallerSha256,
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "SHA-256 загруженного установщика не совпал с GitHub Release.");
                complete = true;
                return path;
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

        private static WebClient CreateClient()
        {
            var client = new WebClient();
            client.Encoding = Encoding.UTF8;
            client.Headers[HttpRequestHeader.UserAgent] =
                "AeroMirror-Windows/" +
                AppVersion.Display;
            client.Headers[HttpRequestHeader.Accept] =
                "application/vnd.github+json";
            client.Headers["X-GitHub-Api-Version"] = "2026-03-10";
            return client;
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
                string[] parts = value.Split('/');
                if (parts.Length == 2 &&
                    IsSafeRepositoryPart(parts[0]) &&
                    IsSafeRepositoryPart(parts[1]))
                    return parts[0] + "/" + parts[1];
            }
            return "";
        }

        private static bool IsSafeRepositoryPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
                return false;
            foreach (char c in value)
            {
                if (!char.IsLetterOrDigit(c) &&
                    c != '-' && c != '_' && c != '.')
                    return false;
            }
            return true;
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
                @"^v?(\d+)\.(\d+)\.(\d+)$",
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
