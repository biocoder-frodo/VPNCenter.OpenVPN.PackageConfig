namespace VPNCenter.OpenVPN.PackageConfig
{
    class Route
    {
        private bool _server;
        public Route(string network, string netmask)
        {
            Network = network;
            NetMask = netmask;
        }
        public Route(Route serverRange, Routes enable)
        {
            _server = enable.SetServerRange;
            Network = serverRange.Network;
            NetMask = serverRange.NetMask;
        }
        public string Network { get; }
        public string NetMask { get; }

        public override string ToString()
        {
            return $"{(_server ? "server" : "route")} {Network} {NetMask}";
        }
        public string ToString(bool compare)
        {
            return compare ? Network + NetMask : ToString();
        }
    }
}
