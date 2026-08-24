namespace AirPlayReceiverMvp
{
    internal sealed class NetworkProfileInfo
    {
        public bool IsKnown;
        public bool IsPublic;
        public string Category = "Unknown";
        public string Name = "";
        public string InterfaceName = "";
        public string Addresses = "";
        public int InterfaceIndex;
        public int NonPhysicalProfileCount;
        public int PublicNonPhysicalProfileCount;
        public string Signature = "Unknown|||";
    }
}
