using System;

namespace AirPlayReceiverMvp
{
    internal sealed class UpdateInfo
    {
        internal Version Version;
        internal string VersionText = "";
        internal string Title = "";
        internal string Notes = "";
        internal string ReleasePage = "";
        internal string InstallerName = "";
        internal string InstallerUrl = "";
        internal string InstallerSha256 = "";
        internal bool IsNewer;
    }
}
