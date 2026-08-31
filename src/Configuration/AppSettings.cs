using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace AirPlayReceiverMvp
{
    internal sealed class AppSettings
    {
        private const int RendererStabilitySettingsVersion = 11;
        internal const int DiscoveryReceiverNameMaxUtf8Bytes = 50;
        private const string DiscoveryReceiverNameFallback = "AeroMirror";
        internal const int CurrentSettingsVersion = 12;

        public int SettingsVersion = CurrentSettingsVersion;
        public string ReceiverName = Environment.MachineName;
        // Legacy fields are read for migration only. AeroMirror now always
        // uses per-device trust with an ephemeral first-connection PIN.
        public string PairingMode = "trust";
        public string FixedPin = "";
        internal string LegacyFixedPinForSanitization = "";
        public string QualityPreset = "1080p60";
        public string Renderer = "d3d11";
        public string LatencyProfile = "balanced";
        public string AudioOutput = "default";
        public string ThemeMode = "system";
        public string AdvancedArguments = "";
        public bool AutoStartReceiver = true;
        public bool AutoStartWindows = true;
        public bool StartMinimized = true;
        public bool CloseToTray = true;
        public bool AutoFitWindow = true;
        public bool AlwaysOnTop = false;
        public bool ShowStreamInTaskbar = true;
        public bool Notify = true;
        public bool AutomaticUpdates = false;
        public bool DismissPinSuggestion = false;
        public int StreamWindowLeft = 0;
        public int StreamWindowTop = 0;
        public int StreamWindowWidth = 0;
        public int StreamWindowHeight = 0;
        public int StreamWindowDpi = 0;

        private static string storageRootForTests;

        internal static void SetStorageRootForTests(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException(
                    "A test storage root is required.", "root");

            string normalizedRoot = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedTempRoot = Path.GetFullPath(
                Path.GetTempPath()).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            DirectoryInfo parent = Directory.GetParent(normalizedRoot);
            Guid rootId;
            if (parent == null ||
                !string.Equals(
                    parent.FullName.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    normalizedTempRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParseExact(
                    Path.GetFileName(normalizedRoot), "N", out rootId))
            {
                throw new ArgumentException(
                    "The test storage root must be a GUID child of the " +
                    "process temporary directory.", "root");
            }

            string existing = Interlocked.CompareExchange(
                ref storageRootForTests, normalizedRoot, null);
            if (existing != null &&
                !string.Equals(
                    existing, normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The process test storage root is already configured.");
            }
            Directory.CreateDirectory(normalizedRoot);
        }

        public static string Folder
        {
            get
            {
                string isolatedRoot = Interlocked.CompareExchange(
                    ref storageRootForTests, null, null);
                if (!string.IsNullOrWhiteSpace(isolatedRoot))
                {
                    Directory.CreateDirectory(isolatedRoot);
                    return isolatedRoot;
                }

                try
                {
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AirPlayReceiverMvp");
                    Directory.CreateDirectory(path);
                    return path;
                }
                catch
                {
                    string portable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                    Directory.CreateDirectory(portable);
                    return portable;
                }
            }
        }

        public static string FilePath { get { return Path.Combine(Folder, "settings.ini"); } }
        public static string LogPath { get { return Path.Combine(Folder, "receiver.log"); } }
        public static string ReceiverKeyPath { get { return Path.Combine(Folder, "receiver-key.pem"); } }
        public static string ReceiverDeviceIdPath { get { return Path.Combine(Folder, "receiver-device-id.txt"); } }
        public static string TrustedClientsPath { get { return Path.Combine(Folder, "trusted-clients.txt"); } }
        public static string TrustResetPendingPath { get { return Path.Combine(Folder, "trust-reset-pending"); } }

        private static readonly object ReceiverDeviceIdSync = new object();
        private static string cachedReceiverDeviceId = "";

        public static string GetSavedReceiverDeviceId()
        {
            lock (ReceiverDeviceIdSync)
            {
                if (IsValidReceiverDeviceId(cachedReceiverDeviceId))
                    return cachedReceiverDeviceId;

                try
                {
                    if (File.Exists(ReceiverDeviceIdPath))
                    {
                        string saved = File.ReadAllText(
                            ReceiverDeviceIdPath, Encoding.UTF8).Trim();
                        if (IsValidReceiverDeviceId(saved))
                        {
                            cachedReceiverDeviceId = saved.ToUpperInvariant();
                            return cachedReceiverDeviceId;
                        }
                    }
                }
                catch { }
                return "";
            }
        }

        public static void RememberReceiverDeviceId(string value)
        {
            if (!IsValidReceiverDeviceId(value))
                return;
            string normalized = value.Trim().ToUpperInvariant();
            lock (ReceiverDeviceIdSync)
            {
                if (IsValidReceiverDeviceId(GetSavedReceiverDeviceId()))
                    return;
                string temporaryPath = ReceiverDeviceIdPath + "." +
                    Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(
                        temporaryPath,
                        normalized + Environment.NewLine,
                        new UTF8Encoding(false));
                    if (File.Exists(ReceiverDeviceIdPath))
                        File.Replace(
                            temporaryPath, ReceiverDeviceIdPath, null, true);
                    else
                        File.Move(temporaryPath, ReceiverDeviceIdPath);
                    cachedReceiverDeviceId = normalized;
                }
                catch { }
                finally
                {
                    try
                    {
                        if (File.Exists(temporaryPath))
                            File.Delete(temporaryPath);
                    }
                    catch { }
                }
            }
        }

        private static bool IsValidReceiverDeviceId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(
                value.Trim(),
                @"^[0-9A-Fa-f]{2}(?::[0-9A-Fa-f]{2}){5}$",
                RegexOptions.CultureInvariant);
        }

        public AppSettings Copy()
        {
            return new AppSettings
            {
                SettingsVersion = SettingsVersion,
                ReceiverName = ReceiverName,
                PairingMode = PairingMode,
                FixedPin = FixedPin,
                QualityPreset = QualityPreset,
                Renderer = Renderer,
                LatencyProfile = LatencyProfile,
                AudioOutput = AudioOutput,
                ThemeMode = ThemeMode,
                AdvancedArguments = AdvancedArguments,
                AutoStartReceiver = AutoStartReceiver,
                AutoStartWindows = AutoStartWindows,
                StartMinimized = StartMinimized,
                CloseToTray = CloseToTray,
                AutoFitWindow = AutoFitWindow,
                AlwaysOnTop = AlwaysOnTop,
                ShowStreamInTaskbar = ShowStreamInTaskbar,
                Notify = Notify,
                AutomaticUpdates = AutomaticUpdates,
                DismissPinSuggestion = DismissPinSuggestion,
                StreamWindowLeft = StreamWindowLeft,
                StreamWindowTop = StreamWindowTop,
                StreamWindowWidth = StreamWindowWidth,
                StreamWindowHeight = StreamWindowHeight,
                StreamWindowDpi = StreamWindowDpi
            };
        }

        public static AppSettings Load()
        {
            var settings = new AppSettings();
            if (!File.Exists(FilePath))
                return settings;

            bool hasSettingsVersion = false;
            bool retiredPairingSettingsFound = false;
            foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                int equals = raw.IndexOf('=');
                if (equals <= 0)
                    continue;
                string key = raw.Substring(0, equals).Trim();
                string value = Unescape(raw.Substring(equals + 1));
                bool flag;
                switch (key)
                {
                    case "SettingsVersion":
                        int version;
                        if (int.TryParse(value, out version))
                        {
                            settings.SettingsVersion = version;
                            hasSettingsVersion = true;
                        }
                        break;
                    case "ReceiverName": settings.ReceiverName = value; break;
                    case "PairingMode":
                        settings.PairingMode = value;
                        retiredPairingSettingsFound = true;
                        break;
                    case "FixedPin":
                        settings.FixedPin = value;
                        retiredPairingSettingsFound = true;
                        break;
                    case "QualityPreset": settings.QualityPreset = value; break;
                    case "Renderer": settings.Renderer = value; break;
                    case "LatencyProfile": settings.LatencyProfile = value; break;
                    case "AudioOutput": settings.AudioOutput = value; break;
                    case "ThemeMode": settings.ThemeMode = value; break;
                    case "AdvancedArguments":
                        settings.AdvancedArguments = value;
                        break;
                    case "AutoStartReceiver":
                        if (bool.TryParse(value, out flag)) settings.AutoStartReceiver = flag;
                        break;
                    case "AutoStartWindows":
                        if (bool.TryParse(value, out flag)) settings.AutoStartWindows = flag;
                        break;
                    case "StartMinimized":
                        if (bool.TryParse(value, out flag)) settings.StartMinimized = flag;
                        break;
                    case "CloseToTray":
                        if (bool.TryParse(value, out flag)) settings.CloseToTray = flag;
                        break;
                    case "AutoFitWindow":
                        if (bool.TryParse(value, out flag)) settings.AutoFitWindow = flag;
                        break;
                    case "FollowPhotosMediaCanvas":
                        // Legacy schema-12 A/B option. The exact observed
                        // Photos/media signature is handled automatically;
                        // retain every other profile value but ignore this
                        // retired key when loading an existing settings.ini.
                        break;
                    case "AlwaysOnTop":
                        if (bool.TryParse(value, out flag)) settings.AlwaysOnTop = flag;
                        break;
                    case "ShowStreamInTaskbar":
                        if (bool.TryParse(value, out flag)) settings.ShowStreamInTaskbar = flag;
                        break;
                    case "Notify":
                        if (bool.TryParse(value, out flag)) settings.Notify = flag;
                        break;
                    case "AutomaticUpdates":
                        if (bool.TryParse(value, out flag)) settings.AutomaticUpdates = flag;
                        break;
                    case "DismissPinSuggestion":
                        if (bool.TryParse(value, out flag)) settings.DismissPinSuggestion = flag;
                        break;
                    case "StreamWindowLeft":
                        int streamWindowLeft;
                        if (int.TryParse(value, out streamWindowLeft))
                            settings.StreamWindowLeft = streamWindowLeft;
                        break;
                    case "StreamWindowTop":
                        int streamWindowTop;
                        if (int.TryParse(value, out streamWindowTop))
                            settings.StreamWindowTop = streamWindowTop;
                        break;
                    case "StreamWindowWidth":
                        int streamWindowWidth;
                        if (int.TryParse(value, out streamWindowWidth))
                            settings.StreamWindowWidth = streamWindowWidth;
                        break;
                    case "StreamWindowHeight":
                        int streamWindowHeight;
                        if (int.TryParse(value, out streamWindowHeight))
                            settings.StreamWindowHeight = streamWindowHeight;
                        break;
                    case "StreamWindowDpi":
                        int streamWindowDpi;
                        if (int.TryParse(value, out streamWindowDpi))
                            settings.StreamWindowDpi = streamWindowDpi;
                        break;
                }
            }

            string loadedAdvancedArguments = settings.AdvancedArguments;
            // v0.2 used an invisible, random PIN as the default. Migrate that
            // exact state to the safer and understandable trusted-network mode.
            if (!hasSettingsVersion)
            {
                if (settings.PairingMode == "pin" && string.IsNullOrWhiteSpace(settings.FixedPin))
                    settings.PairingMode = "none";
                settings.AutoStartWindows = true;
                settings.StartMinimized = true;
            }
            if (!hasSettingsVersion || settings.SettingsVersion < 4)
            {
                // Let GStreamer choose the most stable decoder/sink by default.
                // Explicit D3D12 decoding caused visible stutter on some systems.
                settings.SettingsVersion = 4;
                settings.Renderer = "auto";
                settings.QualityPreset = "1080p60";
                settings.LatencyProfile = "balanced";
            }
            if (!hasSettingsVersion || settings.SettingsVersion < 5)
            {
                settings.SettingsVersion = 5;
                settings.AutoFitWindow = true;
            }
            if (!hasSettingsVersion || settings.SettingsVersion < 6)
            {
                settings.SettingsVersion = 6;
                settings.CloseToTray = true;
            }
            if (!hasSettingsVersion || settings.SettingsVersion < 7)
            {
                settings.SettingsVersion = 7;
                settings.AudioOutput = "default";
                settings.ShowStreamInTaskbar = false;
            }
            if (!hasSettingsVersion || settings.SettingsVersion < 8)
            {
                settings.SettingsVersion = 8;
                settings.ThemeMode = "system";
                settings.ShowStreamInTaskbar = true;
            }
            if (!hasSettingsVersion || settings.SettingsVersion < 9)
            {
                settings.SettingsVersion = 9;
                settings.DismissPinSuggestion = false;
            }
            if (!hasSettingsVersion || settings.SettingsVersion < 10)
            {
                settings.SettingsVersion = 10;
                settings.ClearStreamWindowPlacement();
            }
            MigrateRendererStabilityDefault(settings);
            MigrateCurrentSettingsVersion(settings);
            settings.NormalizePersistedValues();
            retiredPairingSettingsFound |= !string.Equals(
                loadedAdvancedArguments,
                settings.AdvancedArguments,
                StringComparison.Ordinal);
            if (retiredPairingSettingsFound)
            {
                // Remove retired fixed credentials and protected native
                // overrides from disk immediately. Keep startup fail-open if
                // the profile is temporarily read-only; the values are still
                // ignored in memory and never reach the native command line.
                try { settings.Save(); }
                catch { }
            }
            return settings;
        }

        internal static void MigrateRendererStabilityDefault(
            AppSettings settings)
        {
            if (settings == null ||
                settings.SettingsVersion >= RendererStabilitySettingsVersion)
                return;

            if (string.Equals(
                    (settings.Renderer ?? "").Trim(),
                    "auto",
                    StringComparison.OrdinalIgnoreCase))
            {
                settings.Renderer = "d3d11";
            }
            settings.SettingsVersion = RendererStabilitySettingsVersion;
        }

        internal static void MigrateCurrentSettingsVersion(
            AppSettings settings)
        {
            if (settings == null ||
                settings.SettingsVersion >= CurrentSettingsVersion)
                return;

            // Schema 12 no longer exposes its temporary Photos A/B switch.
            // Advancing an older profile changes no unrelated setting; the
            // retired key is ignored independently by the parser above.
            settings.SettingsVersion = CurrentSettingsVersion;
        }

        public void Save()
        {
            NormalizePersistedValues();
            var lines = new[]
            {
                "SettingsVersion=" + SettingsVersion,
                "ReceiverName=" + Escape(ReceiverName),
                "QualityPreset=" + Escape(QualityPreset),
                "Renderer=" + Escape(Renderer),
                "LatencyProfile=" + Escape(LatencyProfile),
                "AudioOutput=" + Escape(AudioOutput),
                "ThemeMode=" + Escape(ThemeMode),
                "AdvancedArguments=" + Escape(AdvancedArguments),
                "AutoStartReceiver=" + AutoStartReceiver,
                "AutoStartWindows=" + AutoStartWindows,
                "StartMinimized=" + StartMinimized,
                "CloseToTray=" + CloseToTray,
                "AutoFitWindow=" + AutoFitWindow,
                "AlwaysOnTop=" + AlwaysOnTop,
                "ShowStreamInTaskbar=" + ShowStreamInTaskbar,
                "Notify=" + Notify,
                "AutomaticUpdates=" + AutomaticUpdates,
                "DismissPinSuggestion=" + DismissPinSuggestion,
                "StreamWindowLeft=" + StreamWindowLeft,
                "StreamWindowTop=" + StreamWindowTop,
                "StreamWindowWidth=" + StreamWindowWidth,
                "StreamWindowHeight=" + StreamWindowHeight,
                "StreamWindowDpi=" + StreamWindowDpi
            };
            WriteAllLinesAtomically(FilePath, lines);
        }

        internal void NormalizePersistedValues()
        {
            ReceiverName = NormalizeReceiverNameForDiscovery(ReceiverName);
            string pin = (FixedPin ?? "").Trim();
            if (IsFourDigitAsciiPin(pin))
                LegacyFixedPinForSanitization = pin;
            if (string.IsNullOrEmpty(LegacyFixedPinForSanitization))
                LegacyFixedPinForSanitization =
                    GetLegacyFourDigitPinFromArguments(AdvancedArguments);
            PairingMode = "trust";
            FixedPin = "";
            AdvancedArguments = RemoveProtectedPairingArguments(
                AdvancedArguments);

            QualityPreset = NormalizeChoice(
                QualityPreset, "1080p60",
                "720p30", "1080p30", "1080p60", "4k60");
            Renderer = NormalizeChoice(
                Renderer, "d3d11", "d3d11", "d3d12");
            LatencyProfile = NormalizeChoice(
                LatencyProfile, "balanced", "balanced", "low", "stable");
            AudioOutput = NormalizeChoice(
                AudioOutput, "default", "default", "mute");
            ThemeMode = NormalizeChoice(
                ThemeMode, "system", "system", "light", "dark");
            if (!HasValidStreamWindowPlacement())
                ClearStreamWindowPlacement();
        }

        internal static string RemoveProtectedPairingArguments(string value)
        {
            string normalized;
            return TryNormalizeAdvancedArguments(value, out normalized)
                ? normalized
                : "";
        }

        internal static bool TryNormalizeAdvancedArguments(
            string value,
            out string normalized)
        {
            normalized = "";
            if (string.IsNullOrWhiteSpace(value))
                return true;
            List<WindowsArgumentToken> tokens;
            if (!TryParseWindowsArguments(value, out tokens))
                return false;
            var retained = new List<string>();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (!IsProtectedPairingOption(tokens[i].Value))
                {
                    retained.Add(QuoteWindowsArgument(tokens[i].Value));
                    continue;
                }

                if (i + 1 < tokens.Count &&
                    !IsProtectedPairingOption(tokens[i + 1].Value))
                {
                    i++;
                }
            }
            normalized = string.Join(" ", retained.ToArray()).Trim();
            return true;
        }

        internal static string RedactProtectedPairingArgumentValues(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? "";
            List<WindowsArgumentToken> tokens;
            if (!TryParseWindowsArguments(value, out tokens))
                return "[invalid advanced arguments]";
            var replacements = new List<WindowsArgumentToken>();
            for (int i = 0; i + 1 < tokens.Count; i++)
            {
                if (IsProtectedPairingOption(tokens[i].Value) &&
                    !IsProtectedPairingOption(tokens[i + 1].Value))
                {
                    replacements.Add(tokens[i + 1]);
                    i++;
                }
            }
            if (replacements.Count == 0)
                return value;

            var redacted = new StringBuilder(value);
            for (int i = replacements.Count - 1; i >= 0; i--)
            {
                WindowsArgumentToken token = replacements[i];
                redacted.Remove(token.Start, token.Length);
                redacted.Insert(token.Start, "****");
            }
            return redacted.ToString();
        }

        private static string GetLegacyFourDigitPinFromArguments(
            string value)
        {
            List<WindowsArgumentToken> tokens;
            if (!TryParseWindowsArguments(value ?? "", out tokens))
                return "";
            for (int i = 0; i + 1 < tokens.Count; i++)
            {
                if (string.Equals(
                        tokens[i].Value, "-pin",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        tokens[i].Value, "--pin",
                        StringComparison.OrdinalIgnoreCase))
                {
                    string candidate = tokens[i + 1].Value;
                    if (IsFourDigitAsciiPin(candidate))
                        return candidate;
                }
            }
            return "";
        }

        private static bool IsProtectedPairingOption(string value)
        {
            return string.Equals(value, "-pin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "--pin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "-pw", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "--pw", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "-reg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "--reg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "-key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "--key", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseWindowsArguments(
            string commandLine,
            out List<WindowsArgumentToken> tokens)
        {
            tokens = new List<WindowsArgumentToken>();
            string text = commandLine ?? "";
            if (text.Length > 4096 || text.IndexOf('\0') >= 0 ||
                text.IndexOf('\r') >= 0 || text.IndexOf('\n') >= 0)
            {
                return false;
            }
            int index = 0;
            while (index < text.Length)
            {
                while (index < text.Length &&
                    (text[index] == ' ' || text[index] == '\t'))
                {
                    index++;
                }
                if (index >= text.Length)
                    break;

                int start = index;
                bool inQuotes = false;
                var decoded = new StringBuilder();
                while (index < text.Length)
                {
                    if (!inQuotes &&
                        (text[index] == ' ' || text[index] == '\t'))
                    {
                        break;
                    }

                    int slashStart = index;
                    while (index < text.Length && text[index] == '\\')
                        index++;
                    int slashCount = index - slashStart;
                    if (index < text.Length && text[index] == '"')
                    {
                        decoded.Append('\\', slashCount / 2);
                        if ((slashCount & 1) != 0)
                        {
                            decoded.Append('"');
                            index++;
                            continue;
                        }

                        if (inQuotes && index + 1 < text.Length &&
                            text[index + 1] == '"')
                        {
                            decoded.Append('"');
                            index += 2;
                            continue;
                        }

                        inQuotes = !inQuotes;
                        index++;
                        continue;
                    }

                    if (slashCount > 0)
                        decoded.Append('\\', slashCount);
                    if (index >= text.Length ||
                        (!inQuotes &&
                            (text[index] == ' ' || text[index] == '\t')))
                    {
                        continue;
                    }
                    decoded.Append(text[index]);
                    index++;
                }

                if (inQuotes)
                {
                    tokens.Clear();
                    return false;
                }

                tokens.Add(new WindowsArgumentToken(
                    decoded.ToString(),
                    text.Substring(start, index - start),
                    start,
                    index - start));
            }
            return true;
        }

        private static string QuoteWindowsArgument(string value)
        {
            if (value == null)
                value = "";
            if (value.Length > 0 &&
                value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                return value;
            }

            var quoted = new StringBuilder(value.Length + 2);
            quoted.Append('"');
            int backslashCount = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }
                if (character == '"')
                {
                    quoted.Append('\\', backslashCount * 2 + 1);
                    quoted.Append('"');
                    backslashCount = 0;
                    continue;
                }
                if (backslashCount != 0)
                {
                    quoted.Append('\\', backslashCount);
                    backslashCount = 0;
                }
                quoted.Append(character);
            }
            quoted.Append('\\', backslashCount * 2);
            quoted.Append('"');
            return quoted.ToString();
        }

        private sealed class WindowsArgumentToken
        {
            internal readonly string Value;
            internal readonly string Raw;
            internal readonly int Start;
            internal readonly int Length;

            internal WindowsArgumentToken(
                string value, string raw, int start, int length)
            {
                Value = value;
                Raw = raw;
                Start = start;
                Length = length;
            }
        }

        internal bool HasValidStreamWindowPlacement()
        {
            return StreamWindowWidth >= 100 && StreamWindowWidth <= 32767 &&
                StreamWindowHeight >= 100 && StreamWindowHeight <= 32767 &&
                StreamWindowLeft >= -100000 && StreamWindowLeft <= 100000 &&
                StreamWindowTop >= -100000 && StreamWindowTop <= 100000 &&
                StreamWindowDpi >= 48 && StreamWindowDpi <= 768;
        }

        internal void ClearStreamWindowPlacement()
        {
            StreamWindowLeft = 0;
            StreamWindowTop = 0;
            StreamWindowWidth = 0;
            StreamWindowHeight = 0;
            StreamWindowDpi = 0;
        }

        private static string NormalizeChoice(
            string value, string fallback, params string[] allowed)
        {
            string normalized = (value ?? "").Trim().ToLowerInvariant();
            foreach (string candidate in allowed)
            {
                if (string.Equals(
                        normalized, candidate, StringComparison.Ordinal))
                    return candidate;
            }
            return fallback;
        }

        internal static string NormalizeReceiverNameForDiscovery(string value)
        {
            var sanitized = new StringBuilder((value ?? "").Length);
            string input = value ?? "";
            for (int index = 0; index < input.Length; index++)
            {
                char character = input[index];
                if (character <= '\u001f' || character == '\u007f')
                    continue;
                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 < input.Length &&
                        char.IsLowSurrogate(input[index + 1]))
                    {
                        sanitized.Append(character);
                        sanitized.Append(input[++index]);
                    }
                    else
                    {
                        sanitized.Append('\ufffd');
                    }
                    continue;
                }
                sanitized.Append(char.IsLowSurrogate(character)
                    ? '\ufffd' : character);
            }

            string candidate = sanitized.ToString().Trim();
            if (candidate.Length == 0)
                return DiscoveryReceiverNameFallback;

            var canonical = new StringBuilder(candidate.Length);
            int usedBytes = 0;
            TextElementEnumerator elements =
                StringInfo.GetTextElementEnumerator(candidate);
            while (elements.MoveNext())
            {
                string element = elements.GetTextElement();
                int elementBytes = Encoding.UTF8.GetByteCount(element);
                if (usedBytes + elementBytes >
                    DiscoveryReceiverNameMaxUtf8Bytes)
                    break;
                canonical.Append(element);
                usedBytes += elementBytes;
            }

            return canonical.Length == 0
                ? DiscoveryReceiverNameFallback
                : canonical.ToString();
        }

        private static bool IsFourDigitAsciiPin(string value)
        {
            if (value == null || value.Length != 4)
                return false;
            foreach (char digit in value)
            {
                if (digit < '0' || digit > '9')
                    return false;
            }
            return true;
        }

        private static void WriteAllLinesAtomically(
            string path, string[] lines)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(
                directory,
                "." + Path.GetFileName(fullPath) + "." +
                Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllLines(
                    temporaryPath, lines, new UTF8Encoding(false));
                if (File.Exists(fullPath))
                    File.Replace(temporaryPath, fullPath, null, true);
                else
                    File.Move(temporaryPath, fullPath);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch { }
            }
        }

        private static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            var result = new StringBuilder();
            bool escaped = false;
            foreach (char c in value)
            {
                if (escaped)
                {
                    if (c == 'r') result.Append('\r');
                    else if (c == 'n') result.Append('\n');
                    else result.Append(c);
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else result.Append(c);
            }
            if (escaped) result.Append('\\');
            return result.ToString();
        }
    }
}
