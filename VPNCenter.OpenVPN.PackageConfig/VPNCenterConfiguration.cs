using DiskStationManager.SecureShell;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace VPNCenter.OpenVPN.PackageConfig
{
    internal class VPNCenterConfiguration
    {
        private const string configName = "vpn_server_openvpn";

        private static readonly Dictionary<string, PropertyInfo> properties = typeof(VPNCenterConfiguration).GetProperties().ToDictionary(k => k.Name, v => v);

        public ProtocolPort PortConfiguration => PortConfigurationAvailable ? _portConfiguration[configName].DestinationPorts.SingleOrDefault() : null;
        public bool PortConfigurationAvailable => IsAvailable(_portConfiguration, configName);

        private readonly Dictionary<string, VPNCenterPortConfiguration> _portConfiguration;
        private static bool IsAvailable(Dictionary<string, VPNCenterPortConfiguration> config, string name)
        {
            return config.ContainsKey(name) && config[name].DestinationPorts is not null
             && config[name].DestinationPorts.Count == 1;
        }
        private readonly Dictionary<string, string> _confValues = new Dictionary<string, string>();

        [SynoVpnConfParameter("runopenvpn")]
        public bool ProtocolEnabled => GetSynoVpnConfValueYesNo();

        [SynoVpnConfParameter("openvpn_push_route")]
        public bool LANAccessEnabled => GetSynoVpnConfValueYesNo();

        [SynoVpnConfParameter("ovpn_enable_ipv6")]
        public bool IPv6Enabled => GetSynoVpnConfValueYesNo();

        [SynoVpnConfParameter("ovpn_auth_conn")]
        public int MaxAuthenticatedConnections => GetSynoVpnConfValue(-1);

        [SynoVpnConfParameter("vpninterface")]
        public string Interface => GetSynoVpnConfValue("n/a");


        // from the openvpn server configuration file
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

        #region property helpers
        private string? GetSynoVpnConfValueAttribute(string? property)
        {
            if (property is null) return null;
            if (properties.ContainsKey(property) == false) return null;
            var prop = properties[property];
            var attribute = prop.GetCustomAttribute<SynoVpnConfParameterAttribute>();
            if (attribute == null) return null;
            return attribute.Name;
        }
        public bool GetSynoVpnConfValueYesNo([CallerMemberName] string property = null)
        {
            return GetSynoVpnConfValue(s => s == "yes", property);
        }
        public string GetSynoVpnConfValue(string defaultValue, [CallerMemberName] string property = null)
        {
            var result = GetSynoVpnConfValueAttribute(property);
            return (result is null) ? defaultValue : _confValues[result];
        }
        public int GetSynoVpnConfValue(int defaultValue, [CallerMemberName] string property = null)
        {
            return GetSynoVpnConfValue(s => { if (int.TryParse(s, out int v)) return v; return defaultValue; }, defaultValue, property);
        }
        public S GetSynoVpnConfValue<S>(Func<string, S> parseFunc, S defaultValue, [CallerMemberName] string property = null) where S : struct
        {
            S? value = GetSynoVpnConfValueNullable(parseFunc, property);
            return value.HasValue ? value.Value : defaultValue;
        }
        public S GetSynoVpnConfValue<S>(Func<string, S> parseFunc, [CallerMemberName] string property = null) where S : struct
        {
            S? value = GetSynoVpnConfValueNullable(parseFunc, property);
            return value.HasValue ? value.Value : default;
        }
        public S? GetSynoVpnConfValueNullable<S>(Func<string, S> parseFunc, [CallerMemberName] string property = null) where S : struct
        {
            string? name = GetSynoVpnConfValueAttribute(property);
            if (name is null) return null;
            if (_confValues.ContainsKey(name))
            {
                return parseFunc(_confValues[name]);
            }
            return null;
        }
        public S? GetSynoVpnConfValueNullable<S>(Func<string, S?> parseFunc, [CallerMemberName] string property = null) where S : struct
        {
            string? name = GetSynoVpnConfValueAttribute(property);
            if (name is null) return null;
            if (_confValues.ContainsKey(name))
            {
                return parseFunc(_confValues[name]);
            }
            return null;
        }
        #endregion
    }
}
