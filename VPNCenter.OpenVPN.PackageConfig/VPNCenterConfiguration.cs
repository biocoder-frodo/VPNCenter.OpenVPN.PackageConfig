using DiskStationManager.SecureShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPNCenter.OpenVPN.PackageConfig
{
    internal class VPNCenterConfiguration
    {
        private const string configName = "vpn_server_openvpn";
        public ProtocolPort PortConfiguration => PortConfigurationAvailable ?_portConfiguration[configName].DestinationPorts.SingleOrDefault() : null;
        public bool PortConfigurationAvailable =>
            _portConfiguration.ContainsKey(configName) && _portConfiguration[configName].DestinationPorts is not null
            && _portConfiguration[configName].DestinationPorts.Count == 1;

        private readonly Dictionary<string, VPNCenterPortConfiguration> _portConfiguration;
        private readonly Dictionary<string, string> _confValues = new Dictionary<string, string>();

        public bool ProtocolEnabled => _confValues.ContainsKey("runopenvpn") ? _confValues["runopenvpn"] == "yes" ? true : false : false;
        public bool LANAccessEnabled => _confValues.ContainsKey("openvpn_push_route") ? _confValues["openvpn_push_route"] == "yes" ? true : false : false;
        public bool IPv6Enabled => _confValues.ContainsKey("ovpn_enable_ipv6") ? _confValues["ovpn_enable_ipv6"] == "yes" ? true : false : false;
        public int MaxAuthenticatedConnections => _confValues.ContainsKey("ovpn_auth_conn") ? int.Parse(_confValues["ovpn_auth_conn"]) : -1;
        public int? MaxClients { get; set; }
        public static VPNCenterConfiguration PrepareConfiguration(ClientSideFiles local, DSMSession session, string path) => new VPNCenterConfiguration(local, session, path);
        private VPNCenterConfiguration(ClientSideFiles local, DSMSession session, string path)
        {
            using (var stream = session.DownloadFile($"{path}/synovpn_port"))
            {
                _portConfiguration = VPNCenterPortConfiguration.ReadConfiguration(stream);
            }
            using (var stream = session.DownloadFile($"{path}/synovpn.conf"))
            {
                using (var sr = new StreamReader(stream))
                {
                    while (sr.EndOfStream == false)
                    {
                        var line = sr.ReadLine();
                        if (line is not null)
                        {
                            if (string.IsNullOrWhiteSpace(line) == false)
                            {
                                var paramValue = line.Split('=');
                                _confValues.Add(paramValue[0], paramValue[1]);
                            }
                        }
                    }
                }
            }

            using (var stream = session.DownloadFile($"{path}/openvpn/openvpn.conf"))
            {
                if (PortConfigurationAvailable)
                {
                    var preconfiguredInDSMUserInterface = new ServerConfigParser(this, PortConfiguration, stream);
                    var appliedTemplate = new ServerConfigParser(this, PortConfiguration, local.ServerTemplateConfiguration, preconfiguredInDSMUserInterface);

                    appliedTemplate.Write(local.ServerConfigurationUser);
                }
                else
                {
                    Console.WriteLine("Port information was not found???");
                }
            }
        }
    }
}
