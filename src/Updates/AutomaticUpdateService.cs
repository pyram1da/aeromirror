using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AirPlayReceiverMvp
{
    internal sealed class PendingAutomaticUpdate
    {
        internal Version Version;
        internal string InstallerPath = "";
        internal string InstallerName = "";
        internal string Sha256 = "";
        internal DateTime StagedUtc;
        internal int LaunchAttempts;
    }

    internal static class AutomaticUpdateService
    {
        private const string StagingFolderName = "automatic-update";
        private const string PendingManifestName = "pending-update.dat";
        private const string RetryManifestName = "retry-update.dat";
        private const int MaximumLaunchAttempts = 3;
        private static readonly TimeSpan MaximumStageAge =
            TimeSpan.FromDays(14);
        private static readonly TimeSpan FailedLaunchRetryDelay =
            TimeSpan.FromHours(6);
        private static readonly byte[] ManifestEntropy = Encoding.UTF8.GetBytes(
            "AeroMirror automatic update staging v1");

        internal static string StagingFolder
        {
            get
            {
                return Path.Combine(
                    AppSettings.Folder, StagingFolderName);
            }
        }

        private static string PendingManifestPath
        {
            get { return Path.Combine(StagingFolder, PendingManifestName); }
        }

        private static string RetryManifestPath
        {
            get { return Path.Combine(StagingFolder, RetryManifestName); }
        }

        internal static void StageVerifiedInstaller(
            UpdateInfo info, string verifiedInstallerPath)
        {
            StageVerifiedInstaller(
                info, verifiedInstallerPath, DateTime.UtcNow);
        }

        internal static void StageVerifiedInstaller(
            UpdateInfo info,
            string verifiedInstallerPath,
            DateTime stagedUtc)
        {
            ValidateStageCandidate(info, verifiedInstallerPath);
            DateTime utc = stagedUtc.ToUniversalTime();
            DateTime retryAfter;
            if (IsCandidateRetryDeferred(info, utc, out retryAfter))
            {
                throw new InvalidOperationException(
                    "Повтор этой версии отложен до " +
                    retryAfter.ToLocalTime().ToString("g") + ".");
            }

            string folder = EnsureSafeStagingFolder();
            string destination = Path.Combine(folder, info.InstallerName);
            EnsureSafeRegularFileOrMissing(destination);
            string temporary = Path.Combine(
                folder,
                "." + info.InstallerName + "." +
                Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.Copy(verifiedInstallerPath, temporary, false);
                string copiedSha256 = UpdateService.ComputeSha256(temporary);
                if (!string.Equals(
                        copiedSha256,
                        info.InstallerSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "SHA-256 установщика изменился во время подготовки обновления.");
                }

                ReplaceFileAtomically(temporary, destination);
                var pending = new PendingAutomaticUpdate
                {
                    Version = info.Version,
                    InstallerPath = destination,
                    InstallerName = info.InstallerName,
                    Sha256 = info.InstallerSha256.ToUpperInvariant(),
                    StagedUtc = utc,
                    LaunchAttempts = 0
                };
                WritePendingManifest(pending);
                DeleteKnownStagingFilesExcept(
                    info.InstallerName, false, true);
            }
            finally
            {
                DeleteRegularFileQuietly(temporary);
            }
        }

        internal static bool HasUsablePendingUpdate(
            Version currentVersion, out Version pendingVersion)
        {
            PendingAutomaticUpdate pending;
            string reason;
            bool usable = TryReadUsablePendingUpdate(
                currentVersion, DateTime.UtcNow, true,
                out pending, out reason);
            pendingVersion = usable ? pending.Version : null;
            return usable;
        }

        internal static bool TryLaunchPendingUpdate(
            Version currentVersion, out string status)
        {
            PendingAutomaticUpdate pending;
            if (!TryAcquirePendingUpdateForLaunch(
                    currentVersion, DateTime.UtcNow,
                    out pending, out status))
            {
                return false;
            }

            try
            {
                using (Process setup = Process.Start(
                    new ProcessStartInfo(pending.InstallerPath)
                    {
                        Arguments = "/automatic-update /delete-source",
                        WorkingDirectory = Path.GetDirectoryName(
                            pending.InstallerPath),
                        UseShellExecute = true
                    }))
                {
                    if (setup == null)
                    {
                        throw new InvalidOperationException(
                            "Windows не вернула запущенный процесс установщика.");
                    }
                }
                status = "Запущено автоматическое обновление до " +
                    pending.Version.ToString(3) + ".";
                return true;
            }
            catch (Exception ex)
            {
                status = "Не удалось запустить подготовленное обновление: " +
                    ex.Message;
                if (pending.LaunchAttempts >= MaximumLaunchAttempts)
                {
                    RecordFailedLaunchRetry(pending, DateTime.UtcNow);
                    ClearPendingUpdateFiles(false);
                }
                return false;
            }
        }

        internal static bool TryAcquirePendingUpdateForLaunch(
            Version currentVersion,
            DateTime utcNow,
            out PendingAutomaticUpdate pending,
            out string reason)
        {
            if (!TryReadUsablePendingUpdate(
                    currentVersion, utcNow, true,
                    out pending, out reason))
            {
                return false;
            }

            pending.LaunchAttempts++;
            try
            {
                WritePendingManifest(pending);
            }
            catch (Exception ex)
            {
                reason = "Не удалось сохранить счётчик запуска обновления: " +
                    ex.Message;
                pending = null;
                return false;
            }
            reason = "Подготовлено обновление " +
                pending.Version.ToString(3) + ".";
            return true;
        }

        internal static bool TryReadUsablePendingUpdate(
            Version currentVersion,
            DateTime utcNow,
            bool cleanupInvalid,
            out PendingAutomaticUpdate pending,
            out string reason)
        {
            return TryReadUsablePendingUpdate(
                currentVersion, utcNow, cleanupInvalid, false,
                out pending, out reason);
        }

        private static bool TryReadUsablePendingUpdate(
            Version currentVersion,
            DateTime utcNow,
            bool cleanupInvalid,
            bool allowExhaustedAttemptForBusyRecovery,
            out PendingAutomaticUpdate pending,
            out string reason)
        {
            pending = null;
            reason = "Подготовленного обновления нет.";
            if (currentVersion == null)
                throw new ArgumentNullException("currentVersion");

            string folder;
            if (!TryGetSafeStagingFolder(false, out folder) ||
                !File.Exists(Path.Combine(folder, PendingManifestName)))
            {
                if (cleanupInvalid && !string.IsNullOrWhiteSpace(folder))
                    DeleteKnownStagingFilesExcept("", false, false);
                return false;
            }

            try
            {
                EnsureSafeRegularFile(PendingManifestPath);
                pending = ParsePendingManifest(
                    ReadProtectedText(PendingManifestPath));
                DateTime now = utcNow.ToUniversalTime();
                if (pending.Version.CompareTo(currentVersion) <= 0)
                    throw new InvalidDataException(
                        "Подготовленная версия уже установлена или устарела.");
                if (pending.StagedUtc > now.AddMinutes(5) ||
                    now - pending.StagedUtc > MaximumStageAge)
                {
                    throw new InvalidDataException(
                        "Подготовленное обновление устарело.");
                }
                if (pending.LaunchAttempts < 0 ||
                    pending.LaunchAttempts > MaximumLaunchAttempts ||
                    (!allowExhaustedAttemptForBusyRecovery &&
                        pending.LaunchAttempts >= MaximumLaunchAttempts))
                {
                    RecordFailedLaunchRetry(pending, now);
                    throw new InvalidDataException(
                        "Исчерпан лимит запуска подготовленного обновления.");
                }

                string expectedName = "AeroMirror-Setup-" +
                    pending.Version.ToString(3) + ".exe";
                if (!string.Equals(
                        pending.InstallerName,
                        expectedName,
                        StringComparison.Ordinal) ||
                    !UpdateService.IsSha256(pending.Sha256))
                {
                    throw new InvalidDataException(
                        "Метаданные подготовленного обновления некорректны.");
                }
                string expectedPath = Path.Combine(folder, expectedName);
                if (!string.Equals(
                        Path.GetFullPath(pending.InstallerPath),
                        Path.GetFullPath(expectedPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Путь подготовленного установщика некорректен.");
                }
                EnsureSafeRegularFile(expectedPath);
                string actualSha256 = UpdateService.ComputeSha256(expectedPath);
                if (!string.Equals(
                        actualSha256,
                        pending.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "SHA-256 подготовленного установщика не совпал.");
                }

                pending.InstallerPath = expectedPath;
                reason = "Подготовлено обновление " +
                    pending.Version.ToString(3) + ".";
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                pending = null;
                if (cleanupInvalid)
                    ClearPendingUpdateFiles(false);
                return false;
            }
        }

        internal static bool RestorePendingLaunchAttemptAfterSetupBusy(
            Version currentVersion, out string reason)
        {
            return RestorePendingLaunchAttemptAfterSetupBusy(
                currentVersion, DateTime.UtcNow, out reason);
        }

        internal static bool RestorePendingLaunchAttemptAfterSetupBusy(
            Version currentVersion, DateTime utcNow, out string reason)
        {
            PendingAutomaticUpdate pending;
            if (!TryReadUsablePendingUpdate(
                    currentVersion, utcNow, false, true,
                    out pending, out reason))
            {
                return false;
            }
            if (pending.LaunchAttempts <= 0)
            {
                reason = "Счётчик запуска обновления уже восстановлен.";
                return false;
            }
            try
            {
                pending.LaunchAttempts--;
                WritePendingManifest(pending);
                reason = "Попытка обновления не засчитана: другая транзакция " +
                    "Setup была активна.";
                return true;
            }
            catch (Exception ex)
            {
                reason = "Не удалось восстановить счётчик запуска обновления: " +
                    ex.Message;
                return false;
            }
        }

        internal static bool IsCandidateRetryDeferred(
            UpdateInfo info, DateTime utcNow, out DateTime retryAfterUtc)
        {
            retryAfterUtc = DateTime.MinValue;
            string folder;
            if (!TryGetSafeStagingFolder(false, out folder))
                return false;
            string path = Path.Combine(folder, RetryManifestName);
            if (!File.Exists(path))
                return false;

            try
            {
                EnsureSafeRegularFile(path);
                Dictionary<string, string> values = ParseRecord(
                    ReadProtectedText(path));
                Version failedVersion;
                DateTime retryAfter;
                string digest;
                if (!TryParseThreePartVersion(
                        GetRequired(values, "Version"), out failedVersion) ||
                    !UpdateService.IsSha256(
                        digest = GetRequired(values, "Sha256")) ||
                    !DateTime.TryParseExact(
                        GetRequired(values, "RetryAfterUtc"),
                        "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out retryAfter))
                {
                    throw new InvalidDataException(
                        "Некорректная запись повтора обновления.");
                }

                DateTime now = utcNow.ToUniversalTime();
                retryAfter = retryAfter.ToUniversalTime();
                bool sameCandidate = info != null && info.Version != null &&
                    failedVersion.Equals(info.Version) &&
                    string.Equals(
                        digest,
                        info.InstallerSha256,
                        StringComparison.OrdinalIgnoreCase);
                if (!sameCandidate || retryAfter <= now ||
                    retryAfter > now.AddDays(1))
                {
                    DeleteRegularFileQuietly(path);
                    return false;
                }
                retryAfterUtc = retryAfter;
                return true;
            }
            catch
            {
                DeleteRegularFileQuietly(path);
                return false;
            }
        }

        internal static void ClearStagedUpdate()
        {
            ClearPendingUpdateFiles(true);
            CleanupStaleDownloads();
        }

        internal static string CreateDownloadPath(string installerName)
        {
            if (string.IsNullOrWhiteSpace(installerName) || !Regex.IsMatch(
                    installerName,
                    @"^AeroMirror-Setup-\d+\.\d+\.\d+\.exe$",
                    RegexOptions.CultureInvariant))
            {
                throw new InvalidDataException(
                    "Имя загружаемого установщика некорректно.");
            }
            string folder = EnsureSafeStagingFolder();
            string path = Path.Combine(
                folder,
                ".download-" + Guid.NewGuid().ToString("N") + "-" +
                installerName);
            EnsureSafeRegularFileOrMissing(path);
            return path;
        }

        internal static void CleanupStaleDownloads()
        {
            string folder;
            if (!TryGetSafeStagingFolder(false, out folder))
                return;
            string[] paths;
            try { paths = Directory.GetFiles(folder); }
            catch { return; }
            foreach (string path in paths)
            {
                if (IsKnownDownloadName(Path.GetFileName(path)))
                    DeleteRegularFileQuietly(path);
            }
        }

        private static void ValidateStageCandidate(
            UpdateInfo info, string installerPath)
        {
            if (info == null || info.Version == null ||
                info.Version.CompareTo(AppVersion.Current) <= 0)
            {
                throw new InvalidOperationException(
                    "Для подготовки требуется версия новее установленной.");
            }
            string expectedName = "AeroMirror-Setup-" +
                info.Version.ToString(3) + ".exe";
            if (!string.Equals(
                    info.InstallerName,
                    expectedName,
                    StringComparison.Ordinal) ||
                !UpdateService.IsSha256(info.InstallerSha256))
            {
                throw new InvalidDataException(
                    "Имя или SHA-256 установщика не соответствует версии.");
            }
            EnsureSafeRegularFile(installerPath);
            string actual = UpdateService.ComputeSha256(installerPath);
            if (!string.Equals(
                    actual,
                    info.InstallerSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "SHA-256 установщика изменился до подготовки обновления.");
            }
        }

        private static PendingAutomaticUpdate ParsePendingManifest(string text)
        {
            Dictionary<string, string> values = ParseRecord(text);
            Version version;
            DateTime stagedUtc;
            int attempts;
            string sha256 = GetRequired(values, "Sha256");
            if (!TryParseThreePartVersion(
                    GetRequired(values, "Version"), out version) ||
                !DateTime.TryParseExact(
                    GetRequired(values, "StagedUtc"),
                    "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out stagedUtc) ||
                !int.TryParse(
                    GetRequired(values, "LaunchAttempts"),
                    NumberStyles.None, CultureInfo.InvariantCulture,
                    out attempts) ||
                !UpdateService.IsSha256(sha256))
            {
                throw new InvalidDataException(
                    "Метаданные подготовленного обновления повреждены.");
            }

            string installerName = GetRequired(values, "InstallerName");
            return new PendingAutomaticUpdate
            {
                Version = version,
                InstallerName = installerName,
                InstallerPath = Path.Combine(StagingFolder, installerName),
                Sha256 = sha256.ToUpperInvariant(),
                StagedUtc = stagedUtc.ToUniversalTime(),
                LaunchAttempts = attempts
            };
        }

        private static void WritePendingManifest(PendingAutomaticUpdate pending)
        {
            string text =
                "Version=" + pending.Version.ToString(3) + "\n" +
                "InstallerName=" + pending.InstallerName + "\n" +
                "Sha256=" + pending.Sha256.ToUpperInvariant() + "\n" +
                "StagedUtc=" + pending.StagedUtc.ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "LaunchAttempts=" + pending.LaunchAttempts.ToString(
                    CultureInfo.InvariantCulture) + "\n";
            WriteProtectedTextAtomically(PendingManifestPath, text);
        }

        private static void RecordFailedLaunchRetry(
            PendingAutomaticUpdate pending, DateTime utcNow)
        {
            if (pending == null || pending.Version == null ||
                !UpdateService.IsSha256(pending.Sha256))
                return;
            try
            {
                EnsureSafeStagingFolder();
                string text =
                    "Version=" + pending.Version.ToString(3) + "\n" +
                    "Sha256=" + pending.Sha256.ToUpperInvariant() + "\n" +
                    "RetryAfterUtc=" + utcNow.ToUniversalTime()
                        .Add(FailedLaunchRetryDelay)
                        .ToString("O", CultureInfo.InvariantCulture) + "\n";
                WriteProtectedTextAtomically(RetryManifestPath, text);
            }
            catch { }
        }

        private static Dictionary<string, string> ParseRecord(string text)
        {
            var values = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (string rawLine in (text ?? "").Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0)
                    continue;
                int equals = line.IndexOf('=');
                if (equals <= 0 || equals == line.Length - 1)
                    throw new InvalidDataException("Некорректная запись обновления.");
                string key = line.Substring(0, equals);
                string value = line.Substring(equals + 1);
                if (values.ContainsKey(key))
                    throw new InvalidDataException("Повторяющееся поле обновления.");
                values.Add(key, value);
            }
            return values;
        }

        private static string GetRequired(
            Dictionary<string, string> values, string key)
        {
            string value;
            if (!values.TryGetValue(key, out value) ||
                string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    "В записи обновления отсутствует поле " + key + ".");
            }
            return value;
        }

        private static bool TryParseThreePartVersion(
            string text, out Version version)
        {
            string value = (text ?? "").Trim();
            if (!Regex.IsMatch(
                    value,
                    @"^\d+\.\d+\.\d+$",
                    RegexOptions.CultureInvariant))
            {
                version = null;
                return false;
            }
            return Version.TryParse(value, out version);
        }

        private static string ReadProtectedText(string path)
        {
            byte[] protectedBytes = File.ReadAllBytes(path);
            byte[] plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                ManifestEntropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }

        private static void WriteProtectedTextAtomically(
            string path, string text)
        {
            EnsureSafeStagingFolder();
            EnsureSafeRegularFileOrMissing(path);
            byte[] protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(text),
                ManifestEntropy,
                DataProtectionScope.CurrentUser);
            string temporary = Path.Combine(
                Path.GetDirectoryName(path),
                "." + Path.GetFileName(path) + "." +
                Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(temporary, protectedBytes);
                ReplaceFileAtomically(temporary, path);
            }
            finally
            {
                DeleteRegularFileQuietly(temporary);
            }
        }

        private static void ReplaceFileAtomically(
            string temporaryPath, string destinationPath)
        {
            EnsureSafeRegularFile(temporaryPath);
            EnsureSafeRegularFileOrMissing(destinationPath);
            if (File.Exists(destinationPath))
                File.Replace(temporaryPath, destinationPath, null, true);
            else
                File.Move(temporaryPath, destinationPath);
        }

        private static string EnsureSafeStagingFolder()
        {
            string folder;
            if (!TryGetSafeStagingFolder(true, out folder))
            {
                throw new IOException(
                    "Папка автоматического обновления небезопасна или недоступна.");
            }
            return folder;
        }

        private static bool TryGetSafeStagingFolder(
            bool create, out string folder)
        {
            folder = Path.GetFullPath(StagingFolder);
            try
            {
                if (!Directory.Exists(folder))
                {
                    if (!create)
                        return false;
                    Directory.CreateDirectory(folder);
                }
                return (File.GetAttributes(folder) &
                    FileAttributes.ReparsePoint) == 0;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureSafeRegularFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException(
                    "Файл обновления не найден.", path);
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                (attributes & FileAttributes.Directory) != 0)
            {
                throw new IOException(
                    "Файл обновления не является обычным файлом.");
            }
        }

        private static void EnsureSafeRegularFileOrMissing(string path)
        {
            if (File.Exists(path))
                EnsureSafeRegularFile(path);
        }

        private static void ClearPendingUpdateFiles(bool includeRetry)
        {
            DeleteKnownStagingFilesExcept("", true, includeRetry);
        }

        private static void DeleteKnownStagingFilesExcept(
            string keepInstallerName,
            bool includePending,
            bool includeRetry)
        {
            string folder;
            if (!TryGetSafeStagingFolder(false, out folder))
                return;
            string[] paths;
            try { paths = Directory.GetFiles(folder); }
            catch { return; }
            foreach (string path in paths)
            {
                string name = Path.GetFileName(path);
                bool knownInstaller = Regex.IsMatch(
                    name,
                    @"^AeroMirror-Setup-\d+\.\d+\.\d+\.exe$",
                    RegexOptions.CultureInvariant);
                bool knownTemporary = name.EndsWith(
                        ".tmp", StringComparison.Ordinal) &&
                    (name.StartsWith(
                        ".AeroMirror-Setup-", StringComparison.Ordinal) ||
                     name.StartsWith(
                        ".pending-update.dat.", StringComparison.Ordinal) ||
                     name.StartsWith(
                        ".retry-update.dat.", StringComparison.Ordinal));
                bool delete = knownTemporary ||
                    (includePending && string.Equals(
                        name, PendingManifestName, StringComparison.Ordinal)) ||
                    (includeRetry && string.Equals(
                        name, RetryManifestName, StringComparison.Ordinal)) ||
                    (knownInstaller && !string.Equals(
                        name, keepInstallerName, StringComparison.Ordinal));
                if (delete)
                    DeleteRegularFileQuietly(path);
            }
        }

        private static bool IsKnownDownloadName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && Regex.IsMatch(
                name,
                @"^\.download-[0-9a-f]{32}-AeroMirror-Setup-" +
                @"\d+\.\d+\.\d+\.exe$",
                RegexOptions.CultureInvariant);
        }

        private static void DeleteRegularFileQuietly(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return;
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) == 0 &&
                    (attributes & FileAttributes.Directory) == 0)
                {
                    File.Delete(path);
                }
            }
            catch { }
        }
    }
}
