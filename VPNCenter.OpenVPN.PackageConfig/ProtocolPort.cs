namespace VPNCenter.OpenVPN.PackageConfig
{
    internal class ProtocolPort
    {
        private static string[] _label = Enum.GetNames(typeof(Protocol)).Select(s => "/" + s.ToLower()).ToArray();
        public Protocol Protocol { get; }
        public int Port { get; }

        internal ProtocolPort(string proto, string port)
        {
            Port = int.Parse(port);
            Protocol = Enum.Parse<Protocol>(proto, true);
        }
        public override string ToString() => $"{Port}{_label[(int)Protocol]}";
    }
}
