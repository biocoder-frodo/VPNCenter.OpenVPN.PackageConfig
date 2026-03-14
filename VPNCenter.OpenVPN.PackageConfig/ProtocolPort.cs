using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPNCenter.OpenVPN.PackageConfig
{
    enum Protocol
    {
        UDP,
        TCP,
    }
    internal class ProtocolPort
    {
        public Protocol Protocol { get; }
        public int Port { get; }

        internal ProtocolPort(string proto, string port)
        {
            Port = int.Parse(port);
            Protocol = Enum.Parse<Protocol>(proto, true);
        }
    }
}
