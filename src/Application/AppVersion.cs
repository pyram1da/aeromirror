using System;
using System.Reflection;

namespace AirPlayReceiverMvp
{
    internal static class AppVersion
    {
        public static Version Current
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public static string Display
        {
            get { return Current.ToString(3); }
        }
    }
}
