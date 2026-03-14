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
        public VPNCenterPortConfiguration PortConfiguration => _portConfiguration["vpn_server_openvpn"];

        private readonly Dictionary<string, VPNCenterPortConfiguration> _portConfiguration;
        private readonly Dictionary<string, string> _confValues = new Dictionary<string, string>();
        public static VPNCenterConfiguration ReadConfiguration(DSMSession session, string path) => new VPNCenterConfiguration(session, path);
        private VPNCenterConfiguration(DSMSession session, string path)
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
        }

    }
}
