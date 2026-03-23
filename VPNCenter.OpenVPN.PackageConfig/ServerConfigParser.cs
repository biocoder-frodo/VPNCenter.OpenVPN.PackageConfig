using System.Text.RegularExpressions;

namespace VPNCenter.OpenVPN.PackageConfig
{
    internal class ServerConfigParser
    {
        private static readonly Regex regexRoute = new Regex(@"^push\s+""route\s+(?<net>\S*)\s+(?<mask>\S*)""\s?$");
        private static readonly Regex regexServer = new Regex(@"^server\s+(?<net>\S*)\s+(?<mask>\S*)\s?$");
        private static readonly Regex regexPort = new Regex(@"^port\s+(?<port>[0-9]+)\s?$");
        private static readonly Regex regexMaxClients = new Regex(@"^max-clients\s+(?<count>[0-9]+)\s?$");
        private readonly Routes routes = new Routes();

        private int? Port;

        private List<string> document = new List<string>();

        private static readonly Dictionary<Regex, Action<VPNCenterConfiguration, ServerConfigParser, Match>> parsing = new Dictionary<Regex, Action<VPNCenterConfiguration, ServerConfigParser, Match>>()
        {
            { regexServer,     (c,p,m)=>  p.routes.AddServerSubnet(new Route( m.Groups["net"].Value, m.Groups["mask"].Value)) },
            { regexRoute,      (c,p,m)=>  p.routes.Add(new Route( m.Groups["net"].Value, m.Groups["mask"].Value)) },
            { regexPort,       (c,p,m)=>  p.Port = int.Parse(m.Groups["port"].Value) },
            { regexMaxClients, (c,p,m)=>  c.MaxClients = int.Parse(m.Groups["count"].Value) }
        };
        public void Write(FileInfo output)
        {
            using (var sw = new StreamWriter(new FileStream(output.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.None)))
            {
                foreach (var line in document)
                {
                    sw.Write(line);
                    sw.Write('\n');
                }
            }
        }
        public ServerConfigParser(VPNCenterConfiguration configuration, ProtocolPort portDefinition, FileInfo file, ServerConfigParser? withValues = null)
        {
            using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                ReadContents(configuration, portDefinition, fs, withValues);
            }
        }
        public ServerConfigParser(VPNCenterConfiguration configuration, ProtocolPort portDefinition, Stream stream)
        {
            ReadContents(configuration, portDefinition, stream);
        }
        private void ReadContents(VPNCenterConfiguration configuration, ProtocolPort portDefinition, Stream stream, ServerConfigParser? withValues = null)
        {
            using (var sr = new StreamReader(stream))
            {
                while (sr.EndOfStream == false)
                {
                    var line = sr.ReadLine();
                    if (line is not null)
                    {

                        line = line
                            .KeywordReplace("port", portDefinition.Port)
                            .KeywordReplace("proto", portDefinition.ProtoName(configuration))
                            .KeywordReplace("max-clients", configuration.MaxClients);

                        if (withValues is null)
                        {
                            document.Add(line);
                        }
                        else
                        {
                            if (line.Trim() == "{server}")
                            {
                                document.Add(withValues.routes.Server.ToString());
                            }
                            else if (line.Trim() == "{routes}")
                            {
                                foreach (var route in withValues.routes)
                                {
                                    document.Add($@"push ""{route.Value}""");
                                }
                            }
                            else
                            {
                                document.Add(line);
                            }
                        }

                        foreach (var pair in parsing)
                        {
                            Match m = pair.Key.Match(line);
                            if (m.Success) pair.Value(configuration, this, m);
                        }
                    }
                }
            }
        }
    }
}