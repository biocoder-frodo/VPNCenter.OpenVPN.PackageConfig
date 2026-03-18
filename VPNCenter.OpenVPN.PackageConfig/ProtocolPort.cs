using System.Configuration;
using System.Reflection.Metadata;

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

        public string ProtoName() => ProtoName(this);
        public string ProtoName(VPNCenterConfiguration configuration) => ProtoName(this,configuration);

        private static string ProtoName(ProtocolPort portDefinition, VPNCenterConfiguration configuration)
        {
            if (configuration.IPv6Enabled)
            {
                return (portDefinition.Protocol == Protocol.UDP ? "udp6" : "tcp6-server");
            }
            else
            {
                return (portDefinition.Protocol == Protocol.UDP ? "udp6" : "tcp6-server");
            }
        }
        private static string ProtoName(ProtocolPort portDefinition)
        {
            return portDefinition.Protocol == Protocol.UDP ? "udp" : "tcp-client";
        }
    }
}
