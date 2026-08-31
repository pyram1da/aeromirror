using System;
using System.IO;
using System.Threading;

namespace AirPlayReceiverMvp
{
    internal sealed partial class ReceiverContext
    {
        private int automaticUpdateEpoch;
        private int automaticUpdateCheckRunning;
        private int automaticUpdateShutdown;

        public void SetAutomaticUpdatesEnabled(bool enabled)
        {
            if (settings.AutomaticUpdates == enabled)
                return;

            settings.AutomaticUpdates = enabled;
            settings.Save();
            Interlocked.Increment(ref automaticUpdateEpoch);
            if (!enabled)
            {
                AutomaticUpdateService.ClearStagedUpdate();
                Log("Automatic updates disabled; known staged update files " +
                    "were removed.");
                return;
            }

            Log("Automatic updates enabled. A verified newer Setup may be " +
                "staged, but it will run only at a later safe application start.");
            BeginAutomaticUpdateCheck();
        }

        private void BeginAutomaticUpdateCheck()
        {
            if (!settings.AutomaticUpdates ||
                Interlocked.CompareExchange(
                    ref automaticUpdateShutdown, 0, 0) != 0 ||
                Interlocked.CompareExchange(
                    ref automaticUpdateCheckRunning, 1, 0) != 0)
            {
                return;
            }

            int epoch = Interlocked.CompareExchange(
                ref automaticUpdateEpoch, 0, 0);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string downloadedInstaller = "";
                try
                {
                    if (!IsAutomaticUpdateEpochCurrent(epoch))
                        return;

                    Version pendingVersion;
                    if (AutomaticUpdateService.HasUsablePendingUpdate(
                            AppVersion.Current, out pendingVersion))
                    {
                        Log("Automatic update " +
                            pendingVersion.ToString(3) +
                            " is already staged for a later safe start.");
                        return;
                    }

                    UpdateInfo info = UpdateService.Check();
                    if (!IsAutomaticUpdateEpochCurrent(epoch))
                        return;
                    if (!info.IsNewer)
                    {
                        Log("Automatic update check completed; the installed " +
                            "version is current.");
                        return;
                    }

                    DateTime retryAfter;
                    if (AutomaticUpdateService.IsCandidateRetryDeferred(
                            info, DateTime.UtcNow, out retryAfter))
                    {
                        Log("Automatic update " +
                            info.Version.ToString(3) +
                            " remains in bounded retry backoff until " +
                            retryAfter.ToLocalTime().ToString("s") + ".");
                        return;
                    }

                    downloadedInstaller =
                        UpdateService.DownloadAndVerify(info);
                    if (!IsAutomaticUpdateEpochCurrent(epoch))
                        return;

                    AutomaticUpdateService.StageVerifiedInstaller(
                        info, downloadedInstaller);
                    if (!IsAutomaticUpdateEpochCurrent(epoch))
                    {
                        AutomaticUpdateService.ClearStagedUpdate();
                        return;
                    }
                    Log("Automatic update " +
                        info.Version.ToString(3) +
                        " downloaded, SHA-256 verified, and staged. The active " +
                        "receiver was not interrupted; Setup may run only at " +
                        "the next safe AeroMirror start.");
                }
                catch (Exception ex)
                {
                    if (IsAutomaticUpdateEpochCurrent(epoch))
                    {
                        Log("Automatic update check/staging failed without " +
                            "interrupting the receiver: " + ex.Message);
                    }
                }
                finally
                {
                    DeleteAutomaticUpdateDownloadQuietly(downloadedInstaller);
                    Interlocked.Exchange(
                        ref automaticUpdateCheckRunning, 0);
                    if (settings.AutomaticUpdates &&
                        Interlocked.CompareExchange(
                            ref automaticUpdateShutdown, 0, 0) == 0 &&
                        epoch != Interlocked.CompareExchange(
                            ref automaticUpdateEpoch, 0, 0))
                    {
                        BeginAutomaticUpdateCheck();
                    }
                }
            });
        }

        private bool IsAutomaticUpdateEpochCurrent(int epoch)
        {
            return settings.AutomaticUpdates &&
                Interlocked.CompareExchange(
                    ref automaticUpdateShutdown, 0, 0) == 0 &&
                epoch == Interlocked.CompareExchange(
                    ref automaticUpdateEpoch, 0, 0);
        }

        private void StopAutomaticUpdateWork()
        {
            Interlocked.Exchange(ref automaticUpdateShutdown, 1);
            Interlocked.Increment(ref automaticUpdateEpoch);
        }

        private static void DeleteAutomaticUpdateDownloadQuietly(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }
    }
}
