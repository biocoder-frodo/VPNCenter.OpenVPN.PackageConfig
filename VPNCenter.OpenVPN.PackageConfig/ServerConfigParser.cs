using System.Text.RegularExpressions;


namespace VPNCenter.OpenVPN.PackageConfig
{
    internal class ServerConfigParser
    {
        private static readonly Regex regexRoute = new Regex(@"^push\s+""route\s+(?<net>\S*)\s+(?<mask>\S*)""\s?$");
        private static readonly Regex regexServer = new Regex(@"^server\s+(?<net>\S*)\s+(?<mask>\S*)\s?$");
        private static readonly Regex regexPort = new Regex(@"^port\s+(?<port>[0-9]+)\s?$");
        private Route server;
        private List<Route> routes = new List<Route>();
        private int port;

        private List<string> document = new List<string>();

        public void Write(FileInfo output)
        {
            using (var sw = new StreamWriter(new FileStream(output.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.None)))
            {
                foreach (var line in document)
                {
                    sw.Write(line);
                    sw.Write('\r');
                }
            }

        }
        public ServerConfigParser(FileInfo path, ProtocolPort portDefinition)
        {
            using (var sr = new StreamReader(path.FullName))
            {
                while (sr.EndOfStream == false) 
                {
                    var line = sr.ReadLine();
                    if (line is not null)
                        document.Add(line
                        
                        .Replace("{port}", $"port {portDefinition.Port}")
                        .Replace("{proto}", $"proto {(portDefinition.Protocol == Protocol.UDP ? "udp" : "tcp-server")}"));
                }

            }
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
