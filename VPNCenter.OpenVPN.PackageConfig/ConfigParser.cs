using DiskStationManager.SecureShell;
using Renci.SshNet.Security;
using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VPNCenter.OpenVPN.PackageConfig
{
    internal class ConfigParser
    {
        private static readonly Regex regexRoute = new Regex(@"^push\s+""route\s+(?<net>\S*)\s+(?<mask>\S*)""\s?$");
        private static readonly Regex regexServer = new Regex(@"^server\s+(?<net>\S*)\s+(?<mask>\S*)\s?$");
        private static readonly Regex regexPort = new Regex(@"^port\s+(?<port>[0-9]+)\s?$");
        private Route server;
        private List<Route> routes = new List<Route>();
        private int port;

        public ConfigParser(FileInfo path)
        {
            using (var sr = new StreamReader(path.FullName))
            {
                while (sr.EndOfStream == false)
                {
                    string line = sr.ReadLine();
                    var isServer = regexServer.Match(line);
                    var isRoute = regexRoute.Match(line);
                    var isPort = regexPort.Match(line);

                    if (isServer.Success)
                    {
                        server = new Route() { Network = isServer.Groups["net"].Value, NetMask = isServer.Groups["mask"].Value };
                    }
                    if (isRoute.Success)
                    {
                        routes.Add(new Route() { Network = isRoute.Groups["net"].Value, NetMask = isRoute.Groups["mask"].Value });
                    }
                    if (isPort.Success)
                    {
                        port = int.Parse(isPort.Groups["port"].Value);
                    }
                }
            }

        }
    }
}
